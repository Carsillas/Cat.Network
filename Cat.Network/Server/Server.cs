using Cat.Network.Entities;
using Cat.Network.Generator;
using Cat.Network.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;

namespace Cat.Network.Server;

public class Server : ISerializationContext {


	public IEntityStorage EntityStorage { get; }

	private byte[] OutgoingReliableDataBuffer = new byte[1_000_000]; // 1 MB




	private Dictionary<RequestType, Action<RemoteClient, BinaryReader>> RequestParsers { get; } = new Dictionary<RequestType, Action<RemoteClient, BinaryReader>>();

	private List<RemoteClient> Clients { get; } = new List<RemoteClient>();
	private Dictionary<NetworkEntity, RemoteClient> Owners { get; } = new Dictionary<NetworkEntity, RemoteClient>();
	private Dictionary<RemoteClient, HashSet<NetworkEntity>> OwnedEntities { get; } = new Dictionary<RemoteClient, HashSet<NetworkEntity>>();

	private HashSet<NetworkEntity> OwnerNeedsNotified { get; } = new HashSet<NetworkEntity>();


	private int Time { get; set; }

	public Server(IEntityStorage entityStorage) {
		InitializeNetworkRequestParsers();

		EntityStorage = entityStorage;
	}

	public void AddTransport(ITransport transport, NetworkEntity profileEntity) {

		profileEntity.NetworkID = Guid.NewGuid();
		profileEntity.DestroyWithOwner = true;

		RemoteClient remoteClient = new RemoteClient {
			Transport = transport,
			ProfileEntity = profileEntity
		};

		SetOwner(profileEntity, remoteClient);
		OwnerNeedsNotified.Add(profileEntity);

		Clients.Add(remoteClient);
	}

	protected void RemoveTransport(ITransport transport) {

		RemoteClient client = Clients.FirstOrDefault(c => c.Transport == transport);
		if (client == null) {
			return;
		}

		Clients.Remove(client);
		if (TryGetOwnedEntities(client, out HashSet<NetworkEntity> owned)) {
			// Must be removed before iteration, SetOwner modifies `owned`
			// SetOwner checks for collection when setting to null.
			OwnedEntities.Remove(client);
			foreach (NetworkEntity entity in owned) {
				if (entity.DestroyWithOwner) {
					Despawn(entity);
				} else {
					SetOwner(entity, null);
				}
			}
		}

	}


	private void InitializeNetworkRequestParsers() {
		RequestParsers.Add(RequestType.CreateEntity, HandleCreateEntityRequest);
		RequestParsers.Add(RequestType.UpdateEntity, HandleUpdateEntityRequest);
		RequestParsers.Add(RequestType.DeleteEntity, HandleDeleteEntityRequest);
	}


	protected void Spawn(NetworkEntity entity) {

		entity.NetworkID = Guid.NewGuid();

		EntityStorage.RegisterEntity(entity);
	}

	protected void Despawn(NetworkEntity entity) {
		EntityStorage.UnregisterEntity(entity.NetworkID);
		SetOwner(entity, null);
	}

	protected virtual void PreTick() {

	}


	public void Tick() {
		PreTick();
		Time++;
		PostTick();
	}


	private void PostTick() {

		ProcessIncomingPackets();

		foreach (RemoteClient client in Clients) {
			EntityStorage.ProcessRelevantEntities(client.ProfileEntity, client.EntityProcessor);
		}

		foreach (RemoteClient client in Clients) {

			//foreach (NetworkEntity entity in client.EntitiesToDelete) {
			//	using (MemoryStream stream = new MemoryStream(256)) {
			//		using (BinaryWriter writer = new BinaryWriter(stream)) {
			//			writer.Write((byte)RequestType.DeleteEntity);
			//			writer.Write(entity.NetworkID.ToByteArray());

			//			client.Transport.SendPacket(stream.ToArray());
			//		}
			//	}

			//}

			//foreach (NetworkEntity entity in client.EntitiesToCreate) {
			//	client.Transport.SendPacket(entity.Serializer.GetCreateRequest());
			//}

			//foreach (NetworkEntity entity in client.RelevantEntities) {

			//	if (!TryGetOwner(entity, out RemoteClient owner)) {
			//		SetOwner(entity, client);
			//		NotifyAssignedOwner(client, entity);
			//	}

			//	if (OwnerNeedsNotified.Contains(entity) && TryGetOwner(entity, out owner) && owner == client) {
			//		NotifyAssignedOwner(client, entity);
			//		OwnerNeedsNotified.Remove(entity);
			//	}

			//	if (!client.EntitiesToCreate.Contains(entity)) {
			//		byte[] bytes = entity.Serializer.GetUpdateRequest(Time);
			//		if (bytes != null) {
			//			client.Transport.SendPacket(bytes);
			//		}
			//	}

			//}

		}
	}

	private void ProcessIncomingPackets() {
		foreach (RemoteClient client in Clients) {

			client.Transport.ProcessPackets(Processor);

			void Processor(ReadOnlySpan<byte> bytes) {
				try {
					RequestType requestType = (RequestType)reader.ReadByte();
					if (RequestParsers.TryGetValue(requestType, out Action<RemoteClient, BinaryReader> handler)) {
						handler.Invoke(client, reader);
					} else {
						Console.WriteLine($"Unknown network request type: {requestType}");
					}
				} catch (Exception e) {
					Console.Error.WriteLine(e.Message);
					Console.Error.WriteLine(e.StackTrace);
				}

			}
		}
	}

	private static void NotifyAssignedOwner(RemoteClient client, NetworkEntity entity) {
		using (MemoryStream stream = new MemoryStream(20)) {
			using (BinaryWriter writer = new BinaryWriter(stream)) {

				writer.Write((byte)RequestType.AssignOwner);
				writer.Write(entity.NetworkID.ToByteArray());

				client.Transport.SendPacket(stream.ToArray());
			}
		}
	}

	private bool TryGetOwner(NetworkEntity entity, out RemoteClient owner) {
		return Owners.TryGetValue(entity, out owner);
	}

	private bool TryGetOwnedEntities(RemoteClient owner, out HashSet<NetworkEntity> entities) {
		return OwnedEntities.TryGetValue(owner, out entities);
	}

	private void SetOwner(NetworkEntity entity, RemoteClient owner) {

		if (owner == null) {
			if (TryGetOwner(entity, out RemoteClient previousOwner) && TryGetOwnedEntities(previousOwner, out HashSet<NetworkEntity> owned)) {
				owned.Remove(entity);
			}
			Owners.Remove(entity);
		} else {
			if (TryGetOwner(entity, out RemoteClient previousOwner) &&
				TryGetOwnedEntities(previousOwner, out HashSet<NetworkEntity> previousOwnerEntities)) {
				previousOwnerEntities.Remove(entity);
			}

			if (!TryGetOwnedEntities(owner, out HashSet<NetworkEntity> owned)) {
				owned = new HashSet<NetworkEntity>();
				OwnedEntities[owner] = owned;
			}

			owned.Add(entity);
			Owners[entity] = owner;
		}
	}


	private void HandleCreateEntityRequest(RemoteClient remoteClient, BinaryReader reader) {
		Guid networkID = new Guid(reader.ReadBytes(16));
		string entityTypeName = reader.ReadString();
		Type entityType = null;

		try {
			entityType = Type.GetType(entityTypeName);
		} catch (Exception) { }

		if (entityType != null && typeof(NetworkEntity).IsAssignableFrom(entityType)) {
			NetworkEntity instance = (NetworkEntity)Activator.CreateInstance(entityType);

			instance.NetworkID = networkID;

			instance.Serializer.ReadNetworkProperties(reader);

			EntityStorage.RegisterEntity(instance);

			remoteClient.RelevantEntities.Add(instance);
			SetOwner(instance, remoteClient);
		}
	}


	private void HandleUpdateEntityRequest(RemoteClient remoteClient, BinaryReader reader) {
		Guid networkID = new Guid(reader.ReadBytes(16));

		if (EntityStorage.TryGetEntityByNetworkID(networkID, out NetworkEntity entity)) {
			entity.Serializer.ReadNetworkProperties(reader);
		}
	}

	private void HandleDeleteEntityRequest(RemoteClient remoteClient, BinaryReader Reader) {
		Guid networkID = new Guid(Reader.ReadBytes(16));

		if (EntityStorage.TryGetEntityByNetworkID(networkID, out NetworkEntity entity)) {
			SetOwner(entity, null);
		}
		EntityStorage.UnregisterEntity(networkID);
	}

}
