using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using static Cat.Network.SerializationUtils;

namespace Cat.Network;

public class CatServer : ISerializationContext {


	public IEntityStorage EntityStorage { get; }
	bool ISerializationContext.DeserializeDirtiesProperty => true;

	public int Time { get; private set; }

	private BufferPool BufferPool { get; } = new();
	private Dictionary<NetworkEntity, List<byte[]>> OutgoingRPCBuffers { get; } = new();
	private List<RemoteClient> Clients { get; } = new();


	// TODO maybe use ConditionalWeakTable here? Ideally IEntityStorage is the only holder of entities
	private Dictionary<NetworkEntity, NetworkEntity> Owners { get; } = new();
	private HashSet<NetworkEntity> EntitiesMarkedForClean { get; } = new();


	public CatServer(IEntityStorage entityStorage) {
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
				ExtractPacketHeader(packet, out RequestType requestType, out Guid networkID, out Type type, out ReadOnlySpan<byte> content);

				switch (requestType) {
					case RequestType.AssignOwner:
						Console.WriteLine($"Invalid network request type: {requestType}");
						break;
					case RequestType.CreateEntity:
						HandleCreateEntityRequest(remoteClient, networkID, type, content);
						break;
					case RequestType.UpdateEntity:
						HandleUpdateEntityRequest(remoteClient, networkID, content);
						break;
					case RequestType.DeleteEntity:
						HandleDeleteEntityRequest(remoteClient, networkID);
						break;
					case RequestType.RPC:
						HandleRPCEntityRequest(remoteClient, networkID, content);
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
		entity.NetworkID = Guid.NewGuid();
		((INetworkEntity)entity).SerializationContext = this;
		EntityStorage.RegisterEntity(entity);

		if(ownerProfileEntity != null) {
			Owners.Add(entity, ownerProfileEntity);
		}
	}

	public void Despawn(NetworkEntity entity) {
		EntityStorage.UnregisterEntity(entity.NetworkID);
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

		foreach(NetworkEntity entity in EntitiesMarkedForClean) {
			INetworkEntity iEntity = entity;
			iEntity.Clean();
		}

		BufferPool.FreeAllBuffers();
		BufferPool.FreeAllPools();
		OutgoingRPCBuffers.Clear();
	}

	private void HandleCreateEntityRequest(RemoteClient remoteClient, Guid networkID, Type type, ReadOnlySpan<byte> content) {
		NetworkEntity entity = (NetworkEntity)Activator.CreateInstance(type);
		INetworkEntity iEntity = entity;

		iEntity.SerializationContext = this;
		entity.NetworkID = networkID;

		iEntity.Deserialize(CreateOptions, content);
		EntityStorage.RegisterEntity(entity);
		remoteClient.RegisterSpawnedEntity(entity);
		Owners.Add(entity, remoteClient.ProfileEntity);
	}

	internal bool AssignIfOwnerless(NetworkEntity profileEntity, NetworkEntity entity) {
		if (!Owners.TryGetValue(entity, out NetworkEntity currentOwnerProfileEntity)  // entity is not registered to have an owner or
			|| currentOwnerProfileEntity == null // owner is null or
			|| !EntityStorage.TryGetEntityByNetworkID(currentOwnerProfileEntity.NetworkID, out NetworkEntity registeredCurrentOwner)) { // the owner is not registered (client disconnect?)
			currentOwnerProfileEntity = profileEntity;
			Owners[entity] = profileEntity;
		}

		return profileEntity == currentOwnerProfileEntity;
	}

	internal void UnassignIfOwned(NetworkEntity profileEntity, NetworkEntity entity) {
		if (Owners.TryGetValue(entity, out NetworkEntity currentOwnerProfileEntity) && currentOwnerProfileEntity == profileEntity) {
			Owners.Remove(entity);
		}
	}

	private void HandleUpdateEntityRequest(RemoteClient remoteClient, Guid networkID, ReadOnlySpan<byte> content) {
		if (EntityStorage.TryGetEntityByNetworkID(networkID, out NetworkEntity entity) && remoteClient.OwnedEntities.Contains(entity)) {
			INetworkEntity iEntity = entity;
			iEntity.Deserialize(UpdateOptions, content);
		}
	}

	private void HandleDeleteEntityRequest(RemoteClient remoteClient, Guid networkID) {

		if (EntityStorage.TryGetEntityByNetworkID(networkID, out NetworkEntity entity) &&
			Owners.TryGetValue(entity, out NetworkEntity ownerProfile) &&
			remoteClient.ProfileEntity == ownerProfile) {

			EntityStorage.UnregisterEntity(networkID);
			Owners.Remove(entity);
			((INetworkEntity)entity).SerializationContext = null;
		}
	}
	private void HandleRPCEntityRequest(RemoteClient remoteClient, Guid networkID, ReadOnlySpan<byte> contentBuffer) {

		if (EntityStorage.TryGetEntityByNetworkID(networkID, out NetworkEntity entity) &&
			Owners.TryGetValue(entity, out NetworkEntity ownerProfile) &&
			remoteClient.ProfileEntity != ownerProfile) {

			Span<byte> copy = ((ISerializationContext)this).RentRPCBuffer(entity);

			BinaryPrimitives.WriteInt32LittleEndian(copy, contentBuffer.Length + 16);
			copy = copy.Slice(4);
			entity.NetworkID.TryWriteBytes(copy.Slice(0, 16));
			copy = copy.Slice(16);

			contentBuffer.CopyTo(copy);
		}
	}

	Span<byte> ISerializationContext.RentRPCBuffer(NetworkEntity entity) {
		byte[] buffer = BufferPool.RentBuffer();

		WritePacketHeader(buffer, RequestType.RPC, entity, out Span<byte> contentBuffer);

		if (!OutgoingRPCBuffers.TryGetValue(entity, out List<byte[]> rpcs)) {
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
		INetworkEntity iEntity = entity;
		iEntity.LastDirtyTick = Time;
		EntitiesMarkedForClean.Add(entity);
	}
}
