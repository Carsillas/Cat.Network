using System;
using System.Collections.Generic;
using static Cat.Network.SerializationUtils;

namespace Cat.Network;

public class CatClient : ISerializationContext, IDisposable {

	private ITransport Transport { get; set; }
	public IProxyManager ProxyManager { get; }

	bool ISerializationContext.DeserializeDirtiesProperty => false;
	public int Time { get; private set; }

	private BufferPool BufferPool { get; } = new BufferPool();

	private byte[] OutgoingReliableDataBuffer = new byte[1_000_000];

	private Dictionary<Guid, NetworkEntity> Entities { get; } = new Dictionary<Guid, NetworkEntity>();
	private HashSet<NetworkEntity> EntitiesToSpawn { get; } = new HashSet<NetworkEntity>();
	private HashSet<NetworkEntity> EntitiesToDespawn { get; } = new HashSet<NetworkEntity>();
	private Dictionary<NetworkEntity, List<byte[]>> OutgoingRPCBuffers { get; } = new Dictionary<NetworkEntity, List<byte[]>>();


	public CatClient(IProxyManager proxyManager) {
		ProxyManager = proxyManager;
	}

	public void Connect(ITransport serverTransport) {
		Transport = serverTransport;
	}

	public void Spawn(NetworkEntity entity) {
		entity.NetworkID = Guid.NewGuid();
		INetworkEntity iEntity = entity;
		iEntity.SerializationContext = this;

		Entities.Add(entity.NetworkID, entity);
		EntitiesToSpawn.Add(entity);

		ProxyManager.OnEntityCreated(entity);
		ProxyManager.OnGainedOwnership(entity);

	}

	public void Despawn(NetworkEntity entity) {
		ProxyManager.OnEntityDeleted(entity);
		Entities.Remove(entity.NetworkID);

		INetworkEntity iEntity = entity;
		iEntity.SerializationContext = null;

		if (!EntitiesToSpawn.Remove(entity)) {
			EntitiesToDespawn.Add(entity);
		}
	}

	public bool TryGetEntityByNetworkID(Guid NetworkID, out NetworkEntity entity) {
		return Entities.TryGetValue(NetworkID, out entity);
	}

	protected virtual void Execute() {

	}

	public void Tick() {
		OutgoingRPCBuffers.Clear();
		ProcessIncomingPackets();
		Execute();
		ProcessOutgoingPackets();
		Time++;
	}

	private void ProcessOutgoingPackets() {

		foreach (NetworkEntity entity in Entities.Values) {
			INetworkEntity iEntity = entity;

			if (!entity.IsOwner || iEntity.LastDirtyTick < Time || EntitiesToSpawn.Contains(entity) || EntitiesToDespawn.Contains(entity)) {
				continue;
			}

			int headerLength = WritePacketHeader(OutgoingReliableDataBuffer, RequestType.UpdateEntity, entity, out Span<byte> contentBuffer);
			int contentLength = iEntity.Serialize(UpdateOptions, contentBuffer);

			Transport.SendPacket(OutgoingReliableDataBuffer, headerLength + contentLength);

			foreach (ref NetworkPropertyInfo prop in iEntity.NetworkProperties.AsSpan()) {
				prop.Dirty = false;
			}
		}

		foreach (NetworkEntity entity in EntitiesToSpawn) {
			INetworkEntity iEntity = entity;

			int headerLength = WritePacketHeader(OutgoingReliableDataBuffer, RequestType.CreateEntity, entity, out Span<byte> contentBuffer);
			int contentLength = iEntity.Serialize(CreateOptions, contentBuffer);

			Transport.SendPacket(OutgoingReliableDataBuffer, headerLength + contentLength);
		}

		EntitiesToSpawn.Clear();

		foreach (NetworkEntity entity in EntitiesToDespawn) {
			int headerLength = WritePacketHeader(OutgoingReliableDataBuffer, RequestType.DeleteEntity, entity, out Span<byte> contentBuffer);

			Transport.SendPacket(OutgoingReliableDataBuffer, headerLength);
		}

		EntitiesToDespawn.Clear();
	}

	private void ProcessIncomingPackets() {

		Transport.ProcessPackets(Processor);

		void Processor(ReadOnlySpan<byte> bytes) {
			try {
				ExtractPacketHeader(bytes, out RequestType requestType, out Guid networkID, out Type type, out ReadOnlySpan<byte> content);

				switch (requestType) {
					case RequestType.AssignOwner:
						HandleAssignOwnerRequest(networkID);
						break;
					case RequestType.CreateEntity:
						HandleCreateEntityRequest(networkID, type, content);
						break;
					case RequestType.UpdateEntity:
						HandleUpdateEntityRequest(networkID, content);
						break;
					case RequestType.DeleteEntity:
						HandleDeleteEntityRequest(networkID);
						break;
					default:
						Console.WriteLine($"Unknown network request type: {requestType}");
						break;
				}
			} catch (Exception e) {
				Console.Error.WriteLine(e.Message);
				Console.Error.WriteLine(e.StackTrace);
			}
		}

	}

	private void HandleCreateEntityRequest(Guid networkID, Type type, ReadOnlySpan<byte> content) {
		if (Entities.TryGetValue(networkID, out NetworkEntity existingEntity)) {
			if (!existingEntity.IsOwner) {
				INetworkEntity existingIEntity = existingEntity;
				existingIEntity.Deserialize(UpdateOptions, content);
			}
			return;
		}

		NetworkEntity entity = (NetworkEntity)Activator.CreateInstance(type);
		INetworkEntity iEntity = entity;

		iEntity.SerializationContext = this;
		entity.NetworkID = networkID;
		entity.IsOwner = false;

		iEntity.Deserialize(CreateOptions, content);

		Entities[networkID] = entity;
	}

	private void HandleUpdateEntityRequest(Guid networkID, ReadOnlySpan<byte> content) {
		if (Entities.TryGetValue(networkID, out NetworkEntity entity)) {
			if (entity.IsOwner) {
				return;
			}
			INetworkEntity iEntity = entity;
			iEntity.Deserialize(UpdateOptions, content);
		}
	}

	private void HandleDeleteEntityRequest(Guid networkID) {
		if (Entities.TryGetValue(networkID, out NetworkEntity entity)) {
			INetworkEntity iEntity = entity;
			iEntity.SerializationContext = null;
			ProxyManager.OnEntityDeleted(entity);
		}

		Entities.Remove(networkID);
	}

	private void HandleAssignOwnerRequest(Guid networkID) {
		if (Entities.TryGetValue(networkID, out NetworkEntity entity)) {
			entity.IsOwner = true;
			ProxyManager.OnGainedOwnership(entity);
		}
	}

	public virtual void Dispose() {
		ProxyManager?.Dispose();
	}

	public Span<byte> RentRPCBuffer(NetworkEntity entity) {
		byte[] buffer = BufferPool.RentBuffer();

		if(!OutgoingRPCBuffers.TryGetValue(entity, out List<byte[]> rpcs)) {
			rpcs = new List<byte[]> { };
			OutgoingRPCBuffers.Add(entity, rpcs);
		}

		rpcs.Add(buffer);
	
		return buffer;
	}
}
