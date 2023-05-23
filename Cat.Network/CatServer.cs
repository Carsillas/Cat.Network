using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Cat.Network.SerializationUtils;

namespace Cat.Network;

public class CatServer : ISerializationContext {


	public IEntityStorage EntityStorage { get; }
	bool ISerializationContext.DeserializeDirtiesProperty => true;

	public int Time { get; private set; }

	private BufferPool BufferPool { get; } = new BufferPool();
	private Dictionary<NetworkEntity, List<byte[]>> OutgoingRPCBuffers { get; } = new Dictionary<NetworkEntity, List<byte[]>>();
	private List<RemoteClient> Clients { get; } = new List<RemoteClient>();

	public CatServer(IEntityStorage entityStorage) {

		EntityStorage = entityStorage;
	}

	public void AddTransport(ITransport transport, NetworkEntity profileEntity) {

		profileEntity.DestroyWithOwner = true;

		RemoteClient remoteClient = new RemoteClient(this, transport, profileEntity);

		Spawn(profileEntity, profileEntity);

		Clients.Add(remoteClient);
	}

	protected void RemoveTransport(ITransport transport) {

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


	protected void Spawn(NetworkEntity entity, NetworkEntity ownerProfileEntity = null) {

		entity.NetworkID = Guid.NewGuid();
		((INetworkEntity)entity).SerializationContext = this;
		EntityStorage.RegisterEntity(entity, ownerProfileEntity);
	}

	protected void Despawn(NetworkEntity entity) {
		EntityStorage.UnregisterEntity(entity.NetworkID);
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
		PreExecute();
		Execute();
		PostExecute();
		ProcessOutgoingPackets();
		OutgoingRPCBuffers.Clear();
	}


	private void ProcessOutgoingPackets() {

		foreach (RemoteClient client in Clients) {
			EntityStorage.ProcessRelevantEntities(client.ProfileEntity, client);
		}
	}

	protected void DeliverPacket(ITransport transport, ReadOnlySpan<byte> packet) {
		try {

			RemoteClient client = Clients.First(c => c.Transport == transport);

			ExtractPacketHeader(packet, out RequestType requestType, out Guid networkID, out Type type, out ReadOnlySpan<byte> content);

			switch (requestType) {
				case RequestType.AssignOwner:
					Console.WriteLine($"Invalid network request type: {requestType}");
					break;
				case RequestType.CreateEntity:
					HandleCreateEntityRequest(client, networkID, type, content);
					break;
				case RequestType.UpdateEntity:
					HandleUpdateEntityRequest(client, networkID, content);
					break;
				case RequestType.DeleteEntity:
					HandleDeleteEntityRequest(client, networkID);
					break;
				case RequestType.RPC:
					HandleRPCEntityRequest(client, networkID, content);
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

	private void HandleCreateEntityRequest(RemoteClient remoteClient, Guid networkID, Type type, ReadOnlySpan<byte> content) {
		NetworkEntity entity = (NetworkEntity)Activator.CreateInstance(type);
		INetworkEntity iEntity = entity;

		iEntity.SerializationContext = this;
		entity.NetworkID = networkID;

		iEntity.Deserialize(CreateOptions, content);

		EntityStorage.RegisterEntity(entity, remoteClient.ProfileEntity);
	}


	private void HandleUpdateEntityRequest(RemoteClient remoteClient, Guid networkID, ReadOnlySpan<byte> content) {
		if (EntityStorage.TryGetEntityByNetworkID(networkID, out NetworkEntity entity) && remoteClient.OwnedEntities.Contains(entity)) {
			INetworkEntity iEntity = entity;
			iEntity.Deserialize(UpdateOptions, content);
		}
	}

	private void HandleDeleteEntityRequest(RemoteClient remoteClient, Guid networkID) {

		if (EntityStorage.TryGetEntityByNetworkID(networkID, out NetworkEntity entity) &&
			EntityStorage.TryGetOwner(entity, out NetworkEntity ownerProfile) &&
			remoteClient.ProfileEntity == ownerProfile) {

			EntityStorage.UnregisterEntity(networkID);
			((INetworkEntity)entity).SerializationContext = null;
		}
	}
	private void HandleRPCEntityRequest(RemoteClient remoteClient, Guid networkID, ReadOnlySpan<byte> contentBuffer) {

		if (EntityStorage.TryGetEntityByNetworkID(networkID, out NetworkEntity entity) &&
			EntityStorage.TryGetOwner(entity, out NetworkEntity ownerProfile) &&
			remoteClient.ProfileEntity != ownerProfile) {

			Span<byte> copy = RentRPCBuffer(entity);

			BinaryPrimitives.WriteInt32LittleEndian(copy, contentBuffer.Length + 16);
			copy = copy.Slice(4);
			entity.NetworkID.TryWriteBytes(copy.Slice(0, 16));
			copy = copy.Slice(16);
			contentBuffer.CopyTo(copy);
		}
	}

	public Span<byte> RentRPCBuffer(NetworkEntity entity) {
		byte[] buffer = BufferPool.RentBuffer();

		WritePacketHeader(buffer, RequestType.RPC, entity, out Span<byte> contentBuffer);

		if (!OutgoingRPCBuffers.TryGetValue(entity, out List<byte[]> rpcs)) {
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
