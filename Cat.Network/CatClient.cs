using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using static Cat.Network.CatServer;
using static Cat.Network.SerializationUtils;

namespace Cat.Network;

public class CatClient : ISerializationContext {

	private ITransport Transport { get; set; }
	private ILogger Logger { get; }
	public IProxyManager ProxyManager { get; }

	bool ISerializationContext.DeserializeDirtiesProperty => false;
	public int Time { get; private set; }

	private BufferPool BufferPool { get; } = new();

	private byte[] OutgoingReliableDataBuffer { get; } = new byte[1_000_000];

	private Dictionary<Guid, NetworkEntity> Entities { get; } = new();
	private HashSet<NetworkEntity> EntitiesToSpawn { get; } = new();
	private HashSet<NetworkEntity> EntitiesToDespawn { get; } = new();
	private HashSet<(NetworkEntity Entity, Guid NewOwner)> EntitiesToForfeit { get; } = new();
	private Dictionary<NetworkEntity, List<byte[]>> OutgoingRpcBuffers { get; } = new();

	private HashSet<INetworkEntity> EntitiesMarkedForClean { get; } = new();


	public CatClient(ILogger logger, IProxyManager proxyManager) {
		Logger = logger;
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

		entity.NetworkId = Guid.NewGuid();
		entity.IsOwner = true;
		entity.IsSpawned = true;


		foreach (ref NetworkPropertyInfo prop in ((INetworkEntity)entity).NetworkProperties.AsSpan()) {
			prop.LastSetTick = Time;
			prop.LastUpdateTick = Time;
		}

		INetworkEntity iEntity = entity;
		iEntity.SerializationContext = this;

		Entities.Add(entity.NetworkId, entity);
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
		Entities.Remove(entity.NetworkId);

		entity.IsSpawned = false;

		INetworkEntity iEntity = entity;
		iEntity.SerializationContext = null;

		if (!EntitiesToSpawn.Remove(entity)) {
			EntitiesToDespawn.Add(entity);
		}
	}

	public void Disown(NetworkEntity entity, Guid newOwnerProfileId) {
		
		if (!entity.IsOwner) {
			throw new Exception("Cannot disown an entity not owned by this client!");
		}
		
		if (!entity.IsSpawned) {
			throw new Exception("Cannot disown an entity that is not currently spawned!");
		}
		
		ProxyManager.OnForfeitOwnership(entity);
		entity.IsOwner = false;

		EntitiesToForfeit.Add((entity, newOwnerProfileId));
	}

	public bool TryGetEntityByNetworkId(Guid networkId, out NetworkEntity entity) {
		return Entities.TryGetValue(networkId, out entity);
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

		foreach (var forfeit in EntitiesToForfeit) {
			int headerLength = WritePacketHeader(OutgoingReliableDataBuffer, RequestType.AssignOwner, forfeit.Entity, out Span<byte> contentBuffer);
			int contentLength = 16;
			forfeit.NewOwner.TryWriteBytes(contentBuffer.Slice(0, 16));
			
			Transport.SendPacket(OutgoingReliableDataBuffer, headerLength + contentLength);
		}
		
		EntitiesToForfeit.Clear();

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

		foreach (INetworkEntity entity in EntitiesMarkedForClean) {
			entity.Clean();
		}

		OutgoingRpcBuffers.Clear();
		BufferPool.FreeAllBuffers();
		BufferPool.FreeAllPools();
	}

	private void SendOutgoingRpcs(NetworkEntity entity) {
		List<byte[]> outgoingRpcs = ((ISerializationContext)this).GetOutgoingRpcs(entity);

		if(outgoingRpcs != null) {
			foreach (byte[] rpc in outgoingRpcs) {

				const int clientRpcHeaderLength = 17;
				const int clientRpcContentLengthSlot = 4;
				int length = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(rpc, clientRpcHeaderLength, 4));
				Transport.SendPacket(rpc, length + clientRpcHeaderLength + clientRpcContentLengthSlot);
			}
		}
	}

	private PacketProcessor CachedPacketProcessor { get; }

	private void ProcessPacket(ReadOnlySpan<byte> packet) {
		try {
			ExtractPacketHeader(packet, out RequestType requestType, out Guid networkId, out Type type, out ReadOnlySpan<byte> content);

			switch (requestType) {
				case RequestType.AssignOwner:
					HandleAssignOwnerRequest(networkId);
					break;
				case RequestType.CreateEntity:
					HandleCreateEntityRequest(networkId, type, content);
					break;
				case RequestType.UpdateEntity:
					HandleUpdateEntityRequest(networkId, content);
					break;
				case RequestType.DeleteEntity:
					HandleDeleteEntityRequest(networkId);
					break;
				case RequestType.RPC:
					HandleRpcEntityRequest(networkId, content);
					break;
				default:
					Logger?.LogError($"Unknown network request type: {requestType}");
					break;
			}
		} catch (Exception e) {
			Logger?.LogError(e, "Exception occurred while handling a network packet on the client!");
		}
	}

	private void HandleCreateEntityRequest(Guid networkId, Type type, ReadOnlySpan<byte> content) {
		if (Entities.TryGetValue(networkId, out NetworkEntity existingEntity)) {
			if (!existingEntity.IsOwner) {
				throw new Exception("Received create entity request for an entity that already exists!");
			}
			return;
		}

		NetworkEntity entity = (NetworkEntity)Activator.CreateInstance(type);
		INetworkEntity iEntity = entity;

		iEntity.SerializationContext = this;
		entity.NetworkId = networkId;
		entity.IsOwner = false;
		entity.IsSpawned = true;

		iEntity.Deserialize(CreateOptions, content);

		Entities[networkId] = entity;
		ProxyManager.OnEntityCreated(entity);
	}

	private void HandleUpdateEntityRequest(Guid networkId, ReadOnlySpan<byte> content) {
		if (Entities.TryGetValue(networkId, out NetworkEntity entity)) {
			if (entity.IsOwner) {
				return;
			}
			INetworkEntity iEntity = entity;
			iEntity.Deserialize(UpdateOptions, content);
		}
	}

	private void HandleDeleteEntityRequest(Guid networkId) {
		if (Entities.TryGetValue(networkId, out NetworkEntity entity)) {
			entity.IsSpawned = false;

			INetworkEntity iEntity = entity;
			iEntity.SerializationContext = null;
			ProxyManager.OnEntityDeleted(entity);
		}

		Entities.Remove(networkId);
	}

	private void HandleRpcEntityRequest(Guid networkId, ReadOnlySpan<byte> content) {
		if (Entities.TryGetValue(networkId, out NetworkEntity entity)) {
			INetworkEntity iEntity = entity;

			Guid instigatorId = new Guid(content.Slice(0, 16));
			content = content.Slice(16);

			iEntity.HandleRpcInvocation(instigatorId, content);
		}
	}

	private void HandleAssignOwnerRequest(Guid networkId) {
		if (Entities.TryGetValue(networkId, out NetworkEntity entity)) {
			entity.IsOwner = true;
			ProxyManager.OnGainedOwnership(entity);
		}
	}

	Span<byte> ISerializationContext.RentRpcBuffer(NetworkEntity entity) {
		byte[] buffer = BufferPool.RentBuffer();

		WritePacketHeader(buffer, RequestType.RPC, entity, out Span<byte> contentBuffer);

		if(!OutgoingRpcBuffers.TryGetValue(entity, out List<byte[]> rpcs)) {
			rpcs = BufferPool.RentPool();
			OutgoingRpcBuffers.Add(entity, rpcs);
		}

		rpcs.Add(buffer);
	
		return contentBuffer;
	}

	List<byte[]> ISerializationContext.GetOutgoingRpcs(NetworkEntity entity) {
		if (OutgoingRpcBuffers.TryGetValue(entity, out List<byte[]> buffers)) {
			return buffers;
		}
		return null;
	}

	void ISerializationContext.MarkForClean(INetworkEntity entity) {
		entity.LastDirtyTick = Time;
		EntitiesMarkedForClean.Add(entity);
	}

}
