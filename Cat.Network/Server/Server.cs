using Cat.Network.Entities;
using Cat.Network.Generator;
using Cat.Network.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Cat.Network.Serialization.SerializationUtils;

namespace Cat.Network.Server;

public class CatServer : ISerializationContext {


	public IEntityStorage EntityStorage { get; }
	public IPacketSerializer Serializer { get; }


	private byte[] OutgoingReliableDataBuffer = new byte[1_000_000];


	private Dictionary<RequestType, ServerRequestProcessor> RequestProcessors { get; } = new Dictionary<RequestType, ServerRequestProcessor>();

	private List<RemoteClient> Clients { get; } = new List<RemoteClient>();
	private Dictionary<NetworkEntity, RemoteClient> Owners { get; } = new Dictionary<NetworkEntity, RemoteClient>();
	private Dictionary<RemoteClient, HashSet<NetworkEntity>> OwnedEntities { get; } = new Dictionary<RemoteClient, HashSet<NetworkEntity>>();

	private HashSet<NetworkEntity> OwnerNeedsNotified { get; } = new HashSet<NetworkEntity>();


	private int Time { get; set; }

	public CatServer(IEntityStorage entityStorage, IPacketSerializer serializer) {
		InitializeNetworkRequestParsers();

		EntityStorage = entityStorage;
		Serializer = serializer;
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
		RequestProcessors.Add(RequestType.CreateEntity, HandleCreateEntityRequest);
		RequestProcessors.Add(RequestType.UpdateEntity, HandleUpdateEntityRequest);
		RequestProcessors.Add(RequestType.DeleteEntity, HandleDeleteEntityRequest);
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
					RequestType requestType = (RequestType)bytes[0];
					if (RequestProcessors.TryGetValue(requestType, out ServerRequestProcessor processor)) {
						ExtractPacketHeader(bytes, out Guid networkID, out ReadOnlySpan<byte> content);
						processor.Invoke(client, networkID, content);
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


	private void NotifyAssignedOwner(RemoteClient client, NetworkEntity entity) {

		Packet packet = new PacketWriter(OutgoingReliableDataBuffer).WriteRequestType(RequestType.AssignOwner).WriteTarget(entity).Lock();
		client.Transport.SendPacket(OutgoingReliableDataBuffer, packet.Length);

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


	private void HandleCreateEntityRequest(RemoteClient remoteClient, Guid networkID, ReadOnlySpan<byte> content) {
		NetworkEntity instance = Serializer.CreateEntity(networkID, content);

		EntityStorage.RegisterEntity(instance);

		SetOwner(instance, remoteClient);
	}


	private void HandleUpdateEntityRequest(RemoteClient remoteClient, Guid networkID, ReadOnlySpan<byte> content) {
		if (EntityStorage.TryGetEntityByNetworkID(networkID, out NetworkEntity entity)) {
			Serializer.UpdateEntity(entity, content);
		}
	}

	private void HandleDeleteEntityRequest(RemoteClient remoteClient, Guid networkID, ReadOnlySpan<byte> content) {
		if (EntityStorage.TryGetEntityByNetworkID(networkID, out NetworkEntity entity)) {
			SetOwner(entity, null);
		}
		EntityStorage.UnregisterEntity(networkID);
	}

}
