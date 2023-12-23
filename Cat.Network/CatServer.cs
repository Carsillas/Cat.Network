using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using static Cat.Network.SerializationUtils;

namespace Cat.Network;

public class CatServer : ISerializationContext {
	protected ILogger Logger { get; }
	public IEntityStorage EntityStorage { get; }
	bool ISerializationContext.DeserializeDirtiesProperty => true;

	public int Time { get; private set; }

	private BufferPool BufferPool { get; } = new();
	private Dictionary<NetworkEntity, List<byte[]>> OutgoingRpcBuffers { get; } = new();
	private List<RemoteClient> Clients { get; } = new();


	// TODO maybe use ConditionalWeakTable here? Ideally IEntityStorage is the only holder of entities
	private ConditionalWeakTable<NetworkEntity, NetworkEntity> Owners { get; } = new();
	private HashSet<INetworkEntity> EntitiesMarkedForClean { get; } = new();


	public CatServer(ILogger logger, IEntityStorage entityStorage) {
		Logger = logger;
		EntityStorage = entityStorage;
		EntityStorage.Initialize(this);
	}

	public void AddTransport(ITransport transport, NetworkEntity profileEntity) {

		profileEntity.DestroyWithOwner = true;

		RemoteClient remoteClient = new RemoteClient(this, transport, profileEntity);
		remoteClient.CachedPacketProcessor = ProcessPacket;
		remoteClient.Server = this;

		Spawn(profileEntity, profileEntity);

		Clients.Add(remoteClient);

		void ProcessPacket(ReadOnlySpan<byte> packet) {
			try {
				ExtractPacketHeader(packet, out RequestType requestType, out Guid networkId, out Type type, out ReadOnlySpan<byte> content);

				switch (requestType) {
					case RequestType.AssignOwner:
						HandleAssignOwnerEntityRequest(remoteClient, networkId, content);
						break;
					case RequestType.CreateEntity:
						HandleCreateEntityRequest(remoteClient, networkId, type, content);
						break;
					case RequestType.UpdateEntity:
						HandleUpdateEntityRequest(remoteClient, networkId, content);
						break;
					case RequestType.DeleteEntity:
						HandleDeleteEntityRequest(remoteClient, networkId);
						break;
					case RequestType.RPC:
						HandleRpcEntityRequest(remoteClient, networkId, content);
						break;
					default:
						Logger?.LogError($"Unknown network request type: {requestType}");
						break;
				}
			} catch (Exception e) {
				Logger?.LogError(e, "Exception occurred while handling a network packet on the server!");
			}
		}
	}
	
	public void RemoveTransport(ITransport transport) {

		RemoteClient client = Clients.FirstOrDefault(c => c.Transport == transport);
		if (client == null) {
			return;
		}

		Clients.Remove(client);

		foreach (NetworkEntity entity in client.OwnedEntities) {
			if (entity.DestroyWithOwner) {
				Despawn(entity);
			}
		}
	}


	public void Spawn(NetworkEntity entity, NetworkEntity ownerProfileEntity = null) {
		entity.NetworkId = Guid.NewGuid();
		((INetworkEntity)entity).SerializationContext = this;
		EntityStorage.RegisterEntity(entity);

		if(ownerProfileEntity != null) {
			Owners.Add(entity, ownerProfileEntity);
		}
	}

	public void Despawn(NetworkEntity entity) {
		EntityStorage.UnregisterEntity(entity.NetworkId);
		Owners.Remove(entity);
		((INetworkEntity)entity).SerializationContext = null;
	}

	protected virtual void PreExecute() {

	}

	protected virtual void Execute() {

	}

	protected virtual void PostExecute() {

	}

	public void Tick() {
		Time++;
		ProcessIncomingPackets();

		PreExecute();
		Execute();
		PostExecute();

		ProcessOutgoingPackets();
	}

	private void ProcessIncomingPackets() {
		foreach (RemoteClient client in Clients) {
			client.Transport.ReadIncomingPackets(client.CachedPacketProcessor);
		}
	}

	private void ProcessOutgoingPackets() {

		foreach (RemoteClient client in Clients) {
			EntityStorage.ProcessRelevantEntities(client.ProfileEntity, client);
		}

		foreach(INetworkEntity entity in EntitiesMarkedForClean) {
			entity.Clean();
		}

		BufferPool.FreeAllBuffers();
		BufferPool.FreeAllPools();
		OutgoingRpcBuffers.Clear();
	}
	
	private void HandleAssignOwnerEntityRequest(RemoteClient remoteClient, Guid networkId, ReadOnlySpan<byte> contentBuffer) {
		Guid newOwner = new Guid(contentBuffer.Slice(0, 16));
		
		if (EntityStorage.TryGetEntityByNetworkId(networkId, out NetworkEntity entity) && // entity exists and
		    EntityStorage.TryGetEntityByNetworkId(newOwner, out NetworkEntity newOwnerProfile) && // new owner exists and
		    Owners.TryGetValue(entity, out NetworkEntity ownerProfile) && remoteClient.ProfileEntity == ownerProfile) { // current owner is the instigator of this packet
			// no need to check if the entity is un-owned, only owners can forfeit ownership
			
			remoteClient.UnassignEntity(entity);
			AssignIfOwnerless(newOwnerProfile, entity);
		}
	}

	private void HandleCreateEntityRequest(RemoteClient remoteClient, Guid networkId, Type type, ReadOnlySpan<byte> content) {
		NetworkEntity entity = (NetworkEntity)Activator.CreateInstance(type);
		INetworkEntity iEntity = entity;

		iEntity.SerializationContext = this;
		entity.NetworkId = networkId;

		iEntity.Deserialize(CreateOptions, content);
		EntityStorage.RegisterEntity(entity);
		remoteClient.RegisterSpawnedEntity(entity);
		Owners.Add(entity, remoteClient.ProfileEntity);
	}

	internal bool AssignIfOwnerless(NetworkEntity profileEntity, NetworkEntity entity) {
		if (!Owners.TryGetValue(entity, out NetworkEntity currentOwnerProfileEntity)  // entity is not registered to have an owner or
			|| currentOwnerProfileEntity == null // owner is null or
			|| !EntityStorage.TryGetEntityByNetworkId(currentOwnerProfileEntity.NetworkId, out NetworkEntity registeredCurrentOwner)) { // the owner is not registered (client disconnect?)
			currentOwnerProfileEntity = profileEntity;
			Owners.AddOrUpdate(entity, profileEntity);
		}

		return profileEntity == currentOwnerProfileEntity;
	}

	internal void UnassignIfOwned(NetworkEntity profileEntity, NetworkEntity entity) {
		if (Owners.TryGetValue(entity, out NetworkEntity currentOwnerProfileEntity) && currentOwnerProfileEntity == profileEntity) {
			Owners.Remove(entity);
		}
	}

	private void HandleUpdateEntityRequest(RemoteClient remoteClient, Guid networkId, ReadOnlySpan<byte> content) {
		if (EntityStorage.TryGetEntityByNetworkId(networkId, out NetworkEntity entity) && remoteClient.OwnedEntities.Contains(entity)) {
			INetworkEntity iEntity = entity;
			iEntity.Deserialize(UpdateOptions, content);
		}
	}

	private void HandleDeleteEntityRequest(RemoteClient remoteClient, Guid networkId) {

		if (EntityStorage.TryGetEntityByNetworkId(networkId, out NetworkEntity entity) &&
			Owners.TryGetValue(entity, out NetworkEntity ownerProfile) &&
			remoteClient.ProfileEntity == ownerProfile) {

			EntityStorage.UnregisterEntity(networkId);
			Owners.Remove(entity);
			((INetworkEntity)entity).SerializationContext = null;
		}
	}
	private void HandleRpcEntityRequest(RemoteClient remoteClient, Guid networkId, ReadOnlySpan<byte> contentBuffer) {

		if (EntityStorage.TryGetEntityByNetworkId(networkId, out NetworkEntity entity) &&
			Owners.TryGetValue(entity, out NetworkEntity ownerProfile) &&
			remoteClient.ProfileEntity != ownerProfile) {

			Span<byte> copy = ((ISerializationContext)this).RentRpcBuffer(entity);

			BinaryPrimitives.WriteInt32LittleEndian(copy, contentBuffer.Length + 16);
			copy = copy.Slice(4);
			remoteClient.ProfileEntity.NetworkId.TryWriteBytes(copy.Slice(0, 16));
			copy = copy.Slice(16);

			contentBuffer.CopyTo(copy);
		}
	}

	Span<byte> ISerializationContext.RentRpcBuffer(NetworkEntity entity) {
		byte[] buffer = BufferPool.RentBuffer();

		WritePacketHeader(buffer, RequestType.RPC, entity, out Span<byte> contentBuffer);

		if (!OutgoingRpcBuffers.TryGetValue(entity, out List<byte[]> rpcs)) {
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
