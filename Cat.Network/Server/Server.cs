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
	bool ISerializationContext.DeserializeDirtiesProperty => true;

	public int Time { get; private set; }


	private Dictionary<RequestType, ServerRequestProcessor> RequestProcessors { get; } = new Dictionary<RequestType, ServerRequestProcessor>();
	private List<RemoteClient> Clients { get; } = new List<RemoteClient>();



	public CatServer(IEntityStorage entityStorage) {
		InitializeNetworkRequestParsers();

		EntityStorage = entityStorage;
	}

	public void AddTransport(ITransport transport, NetworkEntity profileEntity) {

		profileEntity.DestroyWithOwner = true;

		RemoteClient remoteClient = new RemoteClient(transport, profileEntity);

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


	private void InitializeNetworkRequestParsers() {
		RequestProcessors.Add(RequestType.CreateEntity, HandleCreateEntityRequest);
		RequestProcessors.Add(RequestType.UpdateEntity, HandleUpdateEntityRequest);
		RequestProcessors.Add(RequestType.DeleteEntity, HandleDeleteEntityRequest);
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

	protected virtual void Execute() {

	}


	public void Tick() {
		Time++;
		ProcessIncomingPackets();
		Execute();
		ProcessOutgoingPackets();
	}


	private void ProcessOutgoingPackets() {

		foreach (RemoteClient client in Clients) {
			EntityStorage.ProcessRelevantEntities(client.ProfileEntity, client);
		}
	}

	private void ProcessIncomingPackets() {
		foreach (RemoteClient client in Clients) {

			client.Transport.ProcessPackets(Processor);

			void Processor(ReadOnlySpan<byte> bytes) {
				try {
					ExtractPacketHeader(bytes, out RequestType requestType, out Guid networkID, out ReadOnlySpan<byte> content);
					if (RequestProcessors.TryGetValue(requestType, out ServerRequestProcessor processor)) {
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


	private void HandleCreateEntityRequest(RemoteClient remoteClient, Guid networkID, ReadOnlySpan<byte> content) {
		int typeNameLength = ReadTypeFullName(content, out Type type);

		NetworkEntity entity = (NetworkEntity) Activator.CreateInstance(type);
		INetworkEntity iEntity = entity;

		entity.NetworkID = networkID;

		iEntity.Deserialize(CreateOptions, content.Slice(typeNameLength));
		iEntity.SerializationContext = this;

		EntityStorage.RegisterEntity(entity, remoteClient.ProfileEntity);
	}


	private void HandleUpdateEntityRequest(RemoteClient remoteClient, Guid networkID, ReadOnlySpan<byte> content) {
		if (EntityStorage.TryGetEntityByNetworkID(networkID, out NetworkEntity entity) && remoteClient.OwnedEntities.Contains(entity)) {
			INetworkEntity iEntity = entity;
			iEntity.Deserialize(UpdateOptions, content);
		}
	}

	private void HandleDeleteEntityRequest(RemoteClient remoteClient, Guid networkID, ReadOnlySpan<byte> content) {
		
		if (EntityStorage.TryGetEntityByNetworkID(networkID, out NetworkEntity entity) && 
			EntityStorage.TryGetOwner(entity, out NetworkEntity ownerProfile) && 
			remoteClient.ProfileEntity == ownerProfile) {

			EntityStorage.UnregisterEntity(networkID);
			((INetworkEntity)entity).SerializationContext = null;
		}
	}

	private bool TryGetClientFromProfile(NetworkEntity profileEntity, out RemoteClient remoteClient) {
		remoteClient = Clients.FirstOrDefault(client => client.ProfileEntity == profileEntity);
		return remoteClient != null;
	}

}
