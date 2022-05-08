using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cat.Network {

	public class Client {

		private int Time { get; set; }
		private ITransport Transport { get; set; }
		public IProxyManager ProxyManager { get; }

		private Dictionary<RequestType, Action<BinaryReader>> RequestParsers { get; } = new Dictionary<RequestType, Action<BinaryReader>>();

		private SerializationContext InternalSerializationContext { get; } = new SerializationContext {
			DeserializeDirtiesProperty = false
		};
		protected ISerializationContext SerializationContext => InternalSerializationContext;

		private Dictionary<Guid, NetworkEntity> Entities { get; } = new Dictionary<Guid, NetworkEntity>();
		private Dictionary<Guid, NetworkEntity> OwnedEntities { get; } = new Dictionary<Guid, NetworkEntity>();

		private HashSet<NetworkEntity> EntitiesToSpawn { get; } = new HashSet<NetworkEntity>();
		private HashSet<NetworkEntity> EntitiesToDespawn { get; } = new HashSet<NetworkEntity>();


		public Client(IProxyManager proxyManager) {
			InitializeNetworkRequestParsers();
			ProxyManager = proxyManager;
		}

		public void Connect(ITransport serverTransport) {
			Transport = serverTransport;
		}

		public void Spawn(NetworkEntity entity) {
			entity.NetworkID = Guid.NewGuid();
			entity.Serializer.InitializeSerializationContext(InternalSerializationContext);

			Entities.Add(entity.NetworkID, entity);
			EntitiesToSpawn.Add(entity);
			OwnedEntities.Add(entity.NetworkID, entity);

			ProxyManager.OnEntityCreated(entity);
			ProxyManager.OnGainedOwnership(entity);

		}

		public void Despawn(NetworkEntity entity) {
			ProxyManager.OnEntityDeleted(entity);
			Entities.Remove(entity.NetworkID);
			OwnedEntities.Remove(entity.NetworkID);

			if (!EntitiesToSpawn.Remove(entity)) {
				EntitiesToDespawn.Add(entity);
			}
		}

		public bool TryGetEntityByNetworkID(Guid NetworkID, out NetworkEntity entity) {
			return Entities.TryGetValue(NetworkID, out entity);
		}

		protected virtual void PreTick() {

		}

		public void Tick() {
			PreTick();

			Time++;

			if(Transport == null) {
				return;
			}

			while (Transport.TryReadPacket(out byte[] bytes)) {
				using (MemoryStream stream = new MemoryStream(bytes)) {
					using (BinaryReader reader = new BinaryReader(stream)) {
						RequestType requestType = (RequestType)reader.ReadByte();
						if (RequestParsers.TryGetValue(requestType, out Action<BinaryReader> handler)) {
							handler.Invoke(reader);
						} else {
							Console.WriteLine($"Unknown network request type: {requestType}");
						}
					}
				}
			}


			foreach (NetworkEntity entity in OwnedEntities.Values) {
				if (EntitiesToSpawn.Contains(entity) || EntitiesToDespawn.Contains(entity) || !entity.Serializer.Dirty) {
					continue;
				}

				byte[] bytes = entity.Serializer.GetUpdateRequest(Time);

				if (bytes != null) {
					Transport.SendPacket(bytes);
				}
			}

			foreach (NetworkEntity entity in Entities.Values) {
				List<byte[]> outgoingRPCs = entity.Serializer.GetOutgoingRPCs();
				foreach (byte[] bytes in outgoingRPCs) {
					Transport.SendPacket(bytes);
				}
			}

			foreach (NetworkEntity entity in EntitiesToSpawn) {
				Transport.SendPacket(entity.Serializer.GetCreateRequest());
			}

			EntitiesToSpawn.Clear();

			foreach (NetworkEntity entity in EntitiesToDespawn) {
				Transport.SendPacket(entity.Serializer.GetDeleteRequest());
			}

			EntitiesToDespawn.Clear();

		}

		private void InitializeNetworkRequestParsers() {
			RequestParsers.Add(RequestType.CreateEntity, HandleCreateEntityRequest);
			RequestParsers.Add(RequestType.UpdateEntity, HandleUpdateEntityRequest);
			RequestParsers.Add(RequestType.DeleteEntity, HandleDeleteEntityRequest);
			RequestParsers.Add(RequestType.RPC, HandleRPCRequest);
			RequestParsers.Add(RequestType.AssignOwner, HandleAssignOwnerRequest);
		}

		private void HandleCreateEntityRequest(BinaryReader reader) {
			Guid networkID = new Guid(reader.ReadBytes(16));
			string entityTypeName = reader.ReadString();
			Type entityType = null;

			if (Entities.TryGetValue(networkID, out NetworkEntity existingEntity)) {
				if (!OwnedEntities.ContainsKey(existingEntity.NetworkID)) {
					existingEntity.Serializer.ReadNetworkProperties(reader);
				}
				return;
			}

			try {
				entityType = Type.GetType(entityTypeName);
			} catch (Exception) { }

			if (entityType != null && typeof(NetworkEntity).IsAssignableFrom(entityType)) {
				NetworkEntity instance = (NetworkEntity)Activator.CreateInstance(entityType);
				instance.NetworkID = networkID;
				instance.Serializer.InitializeSerializationContext(InternalSerializationContext);
				instance.Serializer.ReadNetworkProperties(reader);


				Entities.Add(networkID, instance);
				ProxyManager.OnEntityCreated(instance);
			}
		}


		private void HandleUpdateEntityRequest(BinaryReader reader) {
			Guid networkID = new Guid(reader.ReadBytes(16));

			if (Entities.TryGetValue(networkID, out NetworkEntity entity)) {
				if (OwnedEntities.ContainsKey(entity.NetworkID)) {
					return;
				}

				entity.Serializer.ReadNetworkProperties(reader);
			}
		}

		private void HandleDeleteEntityRequest(BinaryReader reader) {
			Guid networkID = new Guid(reader.ReadBytes(16));

			if (Entities.TryGetValue(networkID, out NetworkEntity entity)) {
				ProxyManager.OnEntityDeleted(entity);
			}

			Entities.Remove(networkID);
			OwnedEntities.Remove(networkID);
		}

		private void HandleRPCRequest(BinaryReader reader) {

			Guid entityNetworkID = new Guid(reader.ReadBytes(16));
			Guid invokerNetworkID = new Guid(reader.ReadBytes(16));

			if (Entities.TryGetValue(entityNetworkID, out NetworkEntity entity)) {

				if (Entities.TryGetValue(invokerNetworkID, out NetworkEntity invoker)) {
					RPCContext.Invoker = invoker;
				}

				try {
					entity.Serializer.HandleIncomingRPCInvocation(reader);
				} finally {
					RPCContext.Invoker = null;
				}
			}
		}

		private void HandleAssignOwnerRequest(BinaryReader reader) {

			Guid entityNetworkID = new Guid(reader.ReadBytes(16));

			if (Entities.TryGetValue(entityNetworkID, out NetworkEntity entity)) {
				OwnedEntities.Add(entityNetworkID, entity);
				ProxyManager.OnGainedOwnership(entity);
			}
		}

	}
}
