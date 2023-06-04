using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using static Cat.Network.CatServer;
using static Cat.Network.SerializationUtils;

namespace Cat.Network;

public class CatClient : ISerializationContext {

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

	private HashSet<NetworkEntity> EntitiesMarkedForClean { get; } = new HashSet<NetworkEntity>();


	public CatClient(IProxyManager proxyManager) {
		ProxyManager = proxyManager;
		CachedPacketProcessor = ProcessPacket;
	}

	public void Connect(ITransport serverTransport) {
		Transport = serverTransport;
	}

	public void Spawn(NetworkEntity entity) {

		if (entity.IsSpawned) {
			throw new Exception("Cannot spawn an already spawned entity!");
		}

		entity.NetworkID = Guid.NewGuid();
		entity.IsOwner = true;
		entity.IsSpawned = true;


		foreach (ref NetworkPropertyInfo prop in ((INetworkEntity)entity).NetworkProperties.AsSpan()) {
			prop.LastDirtyTick = Time;
		}

		INetworkEntity iEntity = entity;
		iEntity.SerializationContext = this;

		Entities.Add(entity.NetworkID, entity);
		EntitiesToSpawn.Add(entity);

		ProxyManager.OnEntityCreated(entity);
		ProxyManager.OnGainedOwnership(entity);

	}

	public void Despawn(NetworkEntity entity) {

		if (!entity.IsOwner) {
			throw new Exception("Cannot despawn an entity not owned by this client!");
		}

		if (!entity.IsSpawned) {
			throw new Exception("Cannot despawn an entity that is not currently spawned!");
		}

		ProxyManager.OnEntityDeleted(entity);
		Entities.Remove(entity.NetworkID);

		entity.IsSpawned = false;

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
	}

	private void ProcessIncomingPackets() {
		Transport.ReadIncomingPackets(CachedPacketProcessor);
	}

	private void ProcessOutgoingPackets() {

		foreach (NetworkEntity entity in Entities.Values) {
			INetworkEntity iEntity = entity;

			if (EntitiesToSpawn.Contains(entity) || EntitiesToDespawn.Contains(entity)) {
				continue;
			}

			if (entity.IsOwner) {
				if(iEntity.LastDirtyTick >= Time) {
					int headerLength = WritePacketHeader(OutgoingReliableDataBuffer, RequestType.UpdateEntity, entity, out Span<byte> contentBuffer);
					int contentLength = iEntity.Serialize(UpdateOptions, contentBuffer);

					Transport.SendPacket(OutgoingReliableDataBuffer, headerLength + contentLength);
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

		foreach (NetworkEntity entity in EntitiesMarkedForClean) {
			INetworkEntity iEntity = entity;
			iEntity.Clean();
		}

		OutgoingRPCBuffers.Clear();
		BufferPool.FreeAllBuffers();
		BufferPool.FreeAllPools();

	}

	private void SendOutgoingRpcs(NetworkEntity entity) {
		List<byte[]> outgoingRpcs = ((ISerializationContext)this).GetOutgoingRpcs(entity);

		if(outgoingRpcs != null) {
			foreach (byte[] rpc in outgoingRpcs) {

				const int ClientRpcHeaderLength = 17;
				const int ClientRpcContentLengthSlot = 4;
				int length = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(rpc, ClientRpcHeaderLength, 4));
				Transport.SendPacket(rpc, length + ClientRpcHeaderLength + ClientRpcContentLengthSlot);
			}
		}
	}

	private PacketProcessor CachedPacketProcessor { get; }

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
				throw new Exception("Received create entity request for an entity that already exists!");
			}
			return;
		}

		NetworkEntity entity = (NetworkEntity)Activator.CreateInstance(type);
		INetworkEntity iEntity = entity;

		iEntity.SerializationContext = this;
		entity.NetworkID = networkID;
		entity.IsOwner = false;
		entity.IsSpawned = true;

		iEntity.Deserialize(CreateOptions, content);

		Entities[networkID] = entity;
		ProxyManager.OnEntityCreated(entity);
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

			if (entity.IsOwner) {
				// ignoring despawn of owned entity
				return;
			}

			entity.IsSpawned = false;

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

	Span<byte> ISerializationContext.RentRPCBuffer(NetworkEntity entity) {
		byte[] buffer = BufferPool.RentBuffer();

		WritePacketHeader(buffer, RequestType.RPC, entity, out Span<byte> contentBuffer);

		if(!OutgoingRPCBuffers.TryGetValue(entity, out List<byte[]> rpcs)) {
			rpcs = BufferPool.RentPool();
			OutgoingRPCBuffers.Add(entity, rpcs);
		}

		rpcs.Add(buffer);
	
		return contentBuffer;
	}

	List<byte[]> ISerializationContext.GetOutgoingRpcs(NetworkEntity entity) {
		if (OutgoingRPCBuffers.TryGetValue(entity, out List<byte[]> buffers)) {
			return buffers;
		}
		return null;
	}

	void ISerializationContext.MarkForClean(NetworkEntity entity) {
		EntitiesMarkedForClean.Add(entity);
	}

}
