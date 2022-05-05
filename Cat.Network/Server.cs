using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Cat.Network {
	public class Server {

		private Dictionary<RequestType, Action<ClientDetails, BinaryReader>> RequestParsers { get; } = new Dictionary<RequestType, Action<ClientDetails, BinaryReader>>();
		private SerializationContext SerializationContext { get; } = new SerializationContext {
			DeserializeDirtiesProperty = true
		};
		private List<ClientDetails> Clients { get; } = new List<ClientDetails>();
		public IEntityStorage EntityStorage { get; }

		private Dictionary<NetworkEntity, ClientDetails> Owners { get; } = new Dictionary<NetworkEntity, ClientDetails>();

		private Dictionary<NetworkEntity, List<byte[]>> OutgoingRPCs { get; } = new Dictionary<NetworkEntity, List<byte[]>>();

		private int Time { get; set; }

		public Server(IEntityStorage entityStorage) {
			InitializeNetworkRequestParsers();
			EntityStorage = entityStorage;
		}

		public void AddTransport(ITransport transport, NetworkEntity profileEntity) {

			profileEntity.NetworkID = Guid.NewGuid();
			profileEntity.Serializer.InitializeSerializationContext(SerializationContext);

			ClientDetails clientDetails = new ClientDetails {
				Transport = transport,
				ProfileEntity = profileEntity
			};

			Owners.Add(profileEntity, clientDetails);

			Clients.Add(clientDetails);
		}

		private void InitializeNetworkRequestParsers() {
			RequestParsers.Add(RequestType.CreateEntity, HandleCreateEntityRequest);
			RequestParsers.Add(RequestType.UpdateEntity, HandleUpdateEntityRequest);
			RequestParsers.Add(RequestType.DeleteEntity, HandleDeleteEntityRequest);
			RequestParsers.Add(RequestType.RPC, HandleRPCRequest);
		}

		public void Tick() {
			PreTick();

			Time++;

			OutgoingRPCs.Clear();

			foreach (ClientDetails client in Clients) {
				while (client.Transport.TryReadPacket(out byte[] request)) {
					using (MemoryStream stream = new MemoryStream(request)) {
						using (BinaryReader reader = new BinaryReader(stream)) {
							RequestType requestType = (RequestType)reader.ReadByte();
							if (RequestParsers.TryGetValue(requestType, out Action<ClientDetails, BinaryReader> handler)) {
								handler.Invoke(client, reader);
							} else {
								Console.WriteLine($"Unknown network request type: {requestType}");
							}
						}
					}

				}
			}


			foreach (ClientDetails client in Clients) {
				client.EntitiesToDelete.Clear();
				client.EntitiesToCreate.Clear();

				HashSet<NetworkEntity> relevantEntities = EntityStorage.GetRelevantEntities(client.ProfileEntity);

				foreach (NetworkEntity entity in Clients.Select(c => c.ProfileEntity)) {
					relevantEntities.Add(entity);
				}

				foreach (NetworkEntity entity in client.RelevantEntities) {
					if (!relevantEntities.Contains(entity)) {
						client.EntitiesToDelete.Add(entity);

						if (Owners.TryGetValue(entity, out ClientDetails owner) && owner == client) {
							Owners.Remove(entity);
						}
					}
				}

				foreach (NetworkEntity entity in client.EntitiesToDelete) {
					client.RelevantEntities.Remove(entity);
				}

				foreach (NetworkEntity entity in relevantEntities) {
					if (!client.RelevantEntities.Contains(entity)) {
						client.EntitiesToCreate.Add(entity);
					}
				}

				foreach (NetworkEntity entity in client.EntitiesToCreate) {
					client.RelevantEntities.Add(entity);
				}

			}

			foreach (ClientDetails client in Clients) {

				foreach (NetworkEntity entity in client.EntitiesToDelete) {
					using (MemoryStream stream = new MemoryStream(256)) {
						using (BinaryWriter writer = new BinaryWriter(stream)) {
							writer.Write((byte)RequestType.DeleteEntity);
							writer.Write(entity.NetworkID.ToByteArray());

							client.Transport.SendPacket(stream.ToArray());
						}
					}

				}

				foreach (NetworkEntity entity in client.EntitiesToCreate) {
					client.Transport.SendPacket(entity.Serializer.GetCreateRequest());
				}

				foreach (NetworkEntity entity in client.RelevantEntities) {

					if (!Owners.ContainsKey(entity)) {
						Owners.Add(entity, client);
						using (MemoryStream stream = new MemoryStream(20)) {
							using (BinaryWriter writer = new BinaryWriter(stream)) {

								writer.Write((byte)RequestType.AssignOwner);
								writer.Write(entity.NetworkID.ToByteArray());

								client.Transport.SendPacket(stream.ToArray());
							}
						}
					}

					if (!client.EntitiesToCreate.Contains(entity)) {
						byte[] bytes = entity.Serializer.GetUpdateRequest(Time);
						if (bytes != null) {
							client.Transport.SendPacket(bytes);
						}
					}

					if (Owners.TryGetValue(entity, out ClientDetails owner) && owner == client && OutgoingRPCs.TryGetValue(entity, out List<byte[]> outgoingRpcs)) {
						foreach (byte[] rpc in outgoingRpcs) {
							client.Transport.SendPacket(rpc);
						}
					}

				}

			}

		}

		protected virtual void PreTick() {

		}


		private void HandleCreateEntityRequest(ClientDetails clientDetails, BinaryReader reader) {
			Guid networkID = new Guid(reader.ReadBytes(16));
			string entityTypeName = reader.ReadString();
			Type entityType = null;

			try {
				entityType = Type.GetType(entityTypeName);
			} catch (Exception) { }

			if (entityType != null && typeof(NetworkEntity).IsAssignableFrom(entityType)) {
				NetworkEntity instance = (NetworkEntity)Activator.CreateInstance(entityType);
				instance.NetworkID = networkID;
				instance.Serializer.InitializeSerializationContext(SerializationContext);
				instance.Serializer.ReadNetworkProperties(reader);
				clientDetails.RelevantEntities.Add(instance);
				Owners.Add(instance, clientDetails);
				EntityStorage.RegisterEntity(instance);
			}
		}


		private void HandleUpdateEntityRequest(ClientDetails clientDetails, BinaryReader reader) {
			Guid networkID = new Guid(reader.ReadBytes(16));

			if (EntityStorage.TryGetEntityByNetworkID(networkID, out NetworkEntity entity)) {
				entity.Serializer.ReadNetworkProperties(reader);
			}
		}

		private void HandleDeleteEntityRequest(ClientDetails clientDetails, BinaryReader Reader) {
			Guid networkID = new Guid(Reader.ReadBytes(16));

			if (EntityStorage.TryGetEntityByNetworkID(networkID, out NetworkEntity entity)) {
				Owners.Remove(entity);
			}
			EntityStorage.UnregisterEntity(networkID);
		}


		private void HandleRPCRequest(ClientDetails clientDetails, BinaryReader reader) {
			Guid networkID = new Guid(reader.ReadBytes(16));

			if (EntityStorage.TryGetEntityByNetworkID(networkID, out NetworkEntity entity)) {

				if (!OutgoingRPCs.TryGetValue(entity, out List<byte[]> rpcs)) {
					rpcs = new List<byte[]>();
					OutgoingRPCs.Add(entity, rpcs);
				}

				using (MemoryStream stream = new MemoryStream((int)reader.BaseStream.Length + 16)) {
					using (BinaryWriter writer = new BinaryWriter(stream)) {
						writer.Write((byte)RequestType.RPC);
						writer.Write(networkID.ToByteArray());
						writer.Write(clientDetails.ProfileEntity.NetworkID.ToByteArray());
						writer.Write(reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position)));

						rpcs.Add(stream.ToArray());
					}
				}

			}
		}


		private class ClientDetails {
			public ITransport Transport { get; set; }

			public NetworkEntity ProfileEntity { get; set; }
			public HashSet<NetworkEntity> RelevantEntities { get; } = new HashSet<NetworkEntity>();
			public HashSet<NetworkEntity> EntitiesToDelete { get; } = new HashSet<NetworkEntity>();
			public HashSet<NetworkEntity> EntitiesToCreate { get; } = new HashSet<NetworkEntity>();

		}

	}
}
