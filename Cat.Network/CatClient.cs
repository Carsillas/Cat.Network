using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
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

	protected virtual void PreExecute() {

	}
	protected virtual void Execute() {

	}
	protected virtual void PostExecute() {

	}

	public void Tick() {
		ProcessIncomingPackets();
		PreExecute();
		Execute();
		PostExecute();
		ProcessOutgoingPackets();
		Time++;
		OutgoingRPCBuffers.Clear();
	}

	private void ProcessIncomingPackets() {
		Transport.ReadIncomingPackets(ProcessPacket);
	
	}

	private void ProcessOutgoingPackets() {

		foreach (NetworkEntity entity in Entities.Values) {
			INetworkEntity iEntity = entity;

			if (EntitiesToSpawn.Contains(entity) || EntitiesToDespawn.Contains(entity)) {
				continue;
			}

			if(entity.IsOwner) {
				if(iEntity.LastDirtyTick >= Time) {
					int headerLength = WritePacketHeader(OutgoingReliableDataBuffer, RequestType.UpdateEntity, entity, out Span<byte> contentBuffer);
					int contentLength = iEntity.Serialize(UpdateOptions, contentBuffer);

					Transport.SendPacket(OutgoingReliableDataBuffer, headerLength + contentLength);

					foreach (ref NetworkPropertyInfo prop in iEntity.NetworkProperties.AsSpan()) {
						prop.Dirty = false;
					}
				}

			} else {
				SendOutgoingRpcs(entity);
			}
		}

		foreach (NetworkEntity entity in EntitiesToSpawn) {
			INetworkEntity iEntity = entity;

			int headerLength = WritePacketHeader(OutgoingReliableDataBuffer, RequestType.CreateEntity, entity, out Span<byte> contentBuffer);
			int contentLength = iEntity.Serialize(CreateOptions, contentBuffer);

			Transport.SendPacket(OutgoingReliableDataBuffer, headerLength + contentLength);
			SendOutgoingRpcs(entity);
		}

		EntitiesToSpawn.Clear();

		foreach (NetworkEntity entity in EntitiesToDespawn) {
			int headerLength = WritePacketHeader(OutgoingReliableDataBuffer, RequestType.DeleteEntity, entity, out Span<byte> contentBuffer);

			Transport.SendPacket(OutgoingReliableDataBuffer, headerLength);
		}

		EntitiesToDespawn.Clear();
	}

	private void SendOutgoingRpcs(NetworkEntity entity) {
		var outgoingRpcs = ((ISerializationContext)this).GetOutgoingRpcs(entity);

		foreach (byte[] rpc in outgoingRpcs) {
			const int ClientRpcHeaderLength = 17;
			const int ClientRpcContentLengthSlot = 4;
			int length = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(rpc, ClientRpcHeaderLength, 4));
			Transport.SendPacket(rpc, length + ClientRpcHeaderLength + ClientRpcContentLengthSlot);
		}
	}

	private void ProcessPacket(ReadOnlySpan<byte> packet) {
		try {
			ExtractPacketHeader(packet, out RequestType requestType, out Guid networkID, out Type type, out ReadOnlySpan<byte> content);

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
				case RequestType.RPC:
					HandleRPCEntityRequest(networkID, content);
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

	private void HandleRPCEntityRequest(Guid networkID, ReadOnlySpan<byte> content) {
		if (Entities.TryGetValue(networkID, out NetworkEntity entity)) {
			INetworkEntity iEntity = entity;

			Guid instigatorId = new Guid(content.Slice(0, 16));
			content = content.Slice(16);

			Entities.TryGetValue(instigatorId, out NetworkEntity instigator);

			iEntity.HandleRPCInvocation(instigator, content);
		}
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

	Span<byte> ISerializationContext.RentRPCBuffer(NetworkEntity entity) {
		byte[] buffer = BufferPool.RentBuffer();

		WritePacketHeader(buffer, RequestType.RPC, entity, out Span<byte> contentBuffer);

		if(!OutgoingRPCBuffers.TryGetValue(entity, out List<byte[]> rpcs)) {
			rpcs = new List<byte[]> { };
			OutgoingRPCBuffers.Add(entity, rpcs);
		}

		rpcs.Add(buffer);
	
		return contentBuffer;
	}

	IEnumerable<byte[]> ISerializationContext.GetOutgoingRpcs(NetworkEntity entity) {
		if (OutgoingRPCBuffers.TryGetValue(entity, out List<byte[]> buffers)) {
			return buffers;
		}
		return Enumerable.Empty<byte[]>();
	}
}
