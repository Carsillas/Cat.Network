using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Cat.Network {
	public class Server {

		private Dictionary<RequestType, Action<ClientDetails, BinaryReader>> RequestParsers { get; } = new Dictionary<RequestType, Action<ClientDetails, BinaryReader>>();
		private SerializationContext InternalSerializationContext { get; } = new SerializationContext {
			DeserializeDirtiesProperty = true
		};

		protected ISerializationContext SerializationContext => InternalSerializationContext;

		private List<ClientDetails> Clients { get; } = new List<ClientDetails>();
		public IEntityStorage EntityStorage { get; }

		private Dictionary<NetworkEntity, ClientDetails> Owners { get; } = new Dictionary<NetworkEntity, ClientDetails>();
		private Dictionary<ClientDetails, HashSet<NetworkEntity>> OwnedEntities { get; } = new Dictionary<ClientDetails, HashSet<NetworkEntity>>();

		private HashSet<NetworkEntity> OwnerNeedsNotified { get; } = new HashSet<NetworkEntity>();


		private Dictionary<NetworkEntity, List<OutgoingRPC>> OutgoingRPCs { get; } = new Dictionary<NetworkEntity, List<OutgoingRPC>>();

		private int Time { get; set; }

		public Server(IEntityStorage entityStorage) {
			InitializeNetworkRequestParsers();
			SerializationContext.RegisterSerializationFunction<NetworkEntity>(SerializeEntityReference, DeserializeEntityReference);

			EntityStorage = entityStorage;
		}

		public void AddTransport(ITransport transport, NetworkEntity profileEntity) {

			profileEntity.NetworkID = Guid.NewGuid();
			profileEntity.DestroyWithOwner.Value = true;
			profileEntity.Serializer.InitializeSerializationContext(InternalSerializationContext);

			ClientDetails clientDetails = new ClientDetails {
				Transport = transport,
				ProfileEntity = profileEntity
			};

			SetOwner(profileEntity, clientDetails);
			OwnerNeedsNotified.Add(profileEntity);

			Clients.Add(clientDetails);
		}

		protected void RemoveTransport(ITransport transport) {

			ClientDetails client = Clients.FirstOrDefault(c => c.Transport == transport);
			if(client == null) {
				return;
			}

			Clients.Remove(client);
			if(TryGetOwnedEntities(client, out HashSet<NetworkEntity> owned)) {
				// Must be removed before iteration, SetOwner modifies `owned`
				// SetOwner checks for collection when setting to null.
				OwnedEntities.Remove(client);
				foreach (NetworkEntity entity in owned) {
					if (entity.DestroyWithOwner.Value) {
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
			RequestParsers.Add(RequestType.RPC, HandleRPCRequest);
			RequestParsers.Add(RequestType.Multicast, HandleMulticastRequest);
		}

		protected virtual void PreTick() {

		}

		protected void Spawn(NetworkEntity entity) {

			entity.NetworkID = Guid.NewGuid();
			entity.Serializer.InitializeSerializationContext(InternalSerializationContext);

			EntityStorage.RegisterEntity(entity);
		}

		protected void Despawn(NetworkEntity entity) {
			EntityStorage.UnregisterEntity(entity.NetworkID);
			SetOwner(entity, null);
		}

		public void Tick() {
			PreTick();

			Time++;

			OutgoingRPCs.Clear();

			foreach (ClientDetails client in Clients) {
				while (client.Transport.TryReadPacket(out byte[] request)) {
					try {
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
					} catch(Exception e) {
						Console.Error.WriteLine(e.Message);
						Console.Error.WriteLine(e.StackTrace);
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

						if (TryGetOwner(entity, out ClientDetails owner) && owner == client) {
							SetOwner(entity, null);
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

					if (!TryGetOwner(entity, out ClientDetails owner)) {
						SetOwner(entity, client);
						NotifyAssignedOwner(client, entity);
					}

					if (OwnerNeedsNotified.Contains(entity) && TryGetOwner(entity, out owner) && owner == client) {
						NotifyAssignedOwner(client, entity);
						OwnerNeedsNotified.Remove(entity);
					}

					if (!client.EntitiesToCreate.Contains(entity)) {
						byte[] bytes = entity.Serializer.GetUpdateRequest(Time);
						if (bytes != null) {
							client.Transport.SendPacket(bytes);
						}
					}

					bool isOwner = TryGetOwner(entity, out owner) && owner == client;

					if (OutgoingRPCs.TryGetValue(entity, out List<OutgoingRPC> outgoingRpcs)) {
						foreach (OutgoingRPC rpc in outgoingRpcs) {
							if(rpc.RequestType == RequestType.RPC && isOwner) {
								client.Transport.SendPacket(rpc.Bytes);
							}
							if(rpc.RequestType == RequestType.Multicast && !isOwner) {
								client.Transport.SendPacket(rpc.Bytes);
							}
						}
					}

				}

			}

		}

		private static void NotifyAssignedOwner(ClientDetails client, NetworkEntity entity) {
			using (MemoryStream stream = new MemoryStream(20)) {
				using (BinaryWriter writer = new BinaryWriter(stream)) {

					writer.Write((byte)RequestType.AssignOwner);
					writer.Write(entity.NetworkID.ToByteArray());

					client.Transport.SendPacket(stream.ToArray());
				}
			}
		}

		private bool TryGetOwner(NetworkEntity entity, out ClientDetails owner) {
			return Owners.TryGetValue(entity, out owner);
		}

		private bool TryGetOwnedEntities(ClientDetails owner, out HashSet<NetworkEntity> entities) {
			return OwnedEntities.TryGetValue(owner, out entities);
		}

		private void SetOwner(NetworkEntity entity, ClientDetails owner) {

			if(owner == null) {
				if(TryGetOwner(entity, out ClientDetails previousOwner) && TryGetOwnedEntities(previousOwner, out HashSet<NetworkEntity> owned)) {
					owned.Remove(entity);
				}
				Owners.Remove(entity);
			} else {
				if(TryGetOwner(entity, out ClientDetails previousOwner) &&
					TryGetOwnedEntities(previousOwner, out HashSet<NetworkEntity> previousOwnerEntities)) {
					previousOwnerEntities.Remove(entity);
				}

				if(!TryGetOwnedEntities(owner, out HashSet<NetworkEntity> owned)) {
					owned = new HashSet<NetworkEntity>();
					OwnedEntities[owner] = owned;
				}

				owned.Add(entity);
				Owners[entity] = owner;
			}
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
				instance.Serializer.InitializeSerializationContext(InternalSerializationContext);

				instance.Serializer.ReadNetworkProperties(reader);

				EntityStorage.RegisterEntity(instance);

				clientDetails.RelevantEntities.Add(instance);
				SetOwner(instance, clientDetails);
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
				SetOwner(entity, null);
			}
			EntityStorage.UnregisterEntity(networkID);
		}


		private void HandleRPCRequest(ClientDetails clientDetails, BinaryReader reader) {
			Guid networkID = new Guid(reader.ReadBytes(16));

			if (EntityStorage.TryGetEntityByNetworkID(networkID, out NetworkEntity entity)) {

				if (!OutgoingRPCs.TryGetValue(entity, out List<OutgoingRPC> rpcs)) {
					rpcs = new List<OutgoingRPC>();
					OutgoingRPCs.Add(entity, rpcs);
				}

				using (MemoryStream stream = new MemoryStream((int)reader.BaseStream.Length + 16)) {
					using (BinaryWriter writer = new BinaryWriter(stream)) {
						writer.Write((byte)RequestType.RPC);
						writer.Write(networkID.ToByteArray());
						writer.Write(clientDetails.ProfileEntity.NetworkID.ToByteArray());
						writer.Write(reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position)));

						rpcs.Add(new OutgoingRPC {
							Bytes = stream.ToArray(),
							RequestType = RequestType.RPC
						});
					}
				}

			}
		}
		private void HandleMulticastRequest(ClientDetails clientDetails, BinaryReader reader) {
			Guid networkID = new Guid(reader.ReadBytes(16));

			if (EntityStorage.TryGetEntityByNetworkID(networkID, out NetworkEntity entity) && TryGetOwner(entity, out ClientDetails owner) && clientDetails == owner) {

				if (!OutgoingRPCs.TryGetValue(entity, out List<OutgoingRPC> rpcs)) {
					rpcs = new List<OutgoingRPC>();
					OutgoingRPCs.Add(entity, rpcs);
				}

				using (MemoryStream stream = new MemoryStream((int)reader.BaseStream.Length + 16)) {
					using (BinaryWriter writer = new BinaryWriter(stream)) {
						writer.Write((byte)RequestType.Multicast);
						writer.Write(networkID.ToByteArray());
						writer.Write(clientDetails.ProfileEntity.NetworkID.ToByteArray());

						long rpcBytesPosition = reader.BaseStream.Position;
						writer.Write(reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position)));
						reader.BaseStream.Position = rpcBytesPosition;

						
						RPCContext.Invoker = clientDetails.ProfileEntity;

						try {
							entity.Serializer.HandleIncomingMulticastInvocation(reader, true);
						} finally {
							RPCContext.Invoker = null;
						}

						rpcs.Add(new OutgoingRPC {
							Bytes = stream.ToArray(),
							RequestType = RequestType.Multicast
						});
					}
				}

			}
		}

		private void SerializeEntityReference(BinaryWriter writer, NetworkEntity value) {
			writer.Write(value != null);
			if (value != null) {
				writer.Write(value.NetworkID.ToByteArray());
			}
		}

		private NetworkEntity DeserializeEntityReference(BinaryReader reader, NetworkProperty<NetworkEntity> networkProperty) {
			bool hasValue = reader.ReadBoolean();
			if (hasValue) {
				Guid networkID = new Guid(reader.ReadBytes(16));

				NetworkEntity result;

				// Must null check because RPC Deserialize function has no property associated.
				if (networkProperty != null) {
					networkProperty.ResolutionFunction = ResolutionFunction;

					NetworkEntity ResolutionFunction() {
						if (EntityStorage.TryGetEntityByNetworkID(networkID, out result)) {
							return result;
						}
						return null;
					}
				}

				if (EntityStorage.TryGetEntityByNetworkID(networkID, out result)) {
					return result;
				}

			} else if (networkProperty != null) {
				networkProperty.ResolutionFunction = null;
			}
			return null;

		}


		private class ClientDetails {
			public ITransport Transport { get; set; }

			public NetworkEntity ProfileEntity { get; set; }
			public HashSet<NetworkEntity> RelevantEntities { get; } = new HashSet<NetworkEntity>();
			public HashSet<NetworkEntity> EntitiesToDelete { get; } = new HashSet<NetworkEntity>();
			public HashSet<NetworkEntity> EntitiesToCreate { get; } = new HashSet<NetworkEntity>();

		}

		private struct OutgoingRPC {
			public byte[] Bytes { get; set; }
			public RequestType RequestType { get; set; }
		
		}

	}
}
