using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cat.Network
{

    public class Client
    {

        private int Time { get; set; }
        private ITransport Transport { get; set; }
        private IProxyManager _ProxyManager { get; set; }
        public IProxyManager ProxyManager { get => _ProxyManager; set => UpdateProxyManager(value); }

        private Dictionary<RequestType, Action<BinaryReader>> RequestParsers { get; } = new Dictionary<RequestType, Action<BinaryReader>>();

        private SerializationContext SerializationContext { get; } = new SerializationContext
        {
            DeserializeDirtiesProperty = false
        };

        private Dictionary<Guid, NetworkEntity> Entities { get; } = new Dictionary<Guid, NetworkEntity>();
        private Dictionary<Guid, NetworkEntity> OwnedEntities { get; } = new Dictionary<Guid, NetworkEntity>();

        private HashSet<NetworkEntity> EntitiesToSpawn { get; } = new HashSet<NetworkEntity>();
        private HashSet<NetworkEntity> EntitiesToDespawn { get; } = new HashSet<NetworkEntity>();
        

        public Client()
        {
            InitializeNetworkRequestParsers();
        }

        public void Connect(ITransport serverTransport)
        {
            Transport = serverTransport;
        }

        public void Spawn(NetworkEntity entity)
        {
            entity.NetworkID = Guid.NewGuid();
            entity.Serializer.InitializeSerializationContext(SerializationContext);

            ProxyManager?.OnEntityCreated(entity);
            Entities.Add(entity.NetworkID, entity);
            EntitiesToSpawn.Add(entity);
            OwnedEntities.Add(entity.NetworkID, entity);
        }

        public void Despawn(NetworkEntity entity)
        {
            ProxyManager?.OnEntityDeleted(entity);
            Entities.Remove(entity.NetworkID);
            OwnedEntities.Remove(entity.NetworkID);

            if (!EntitiesToSpawn.Remove(entity))
            {
                EntitiesToDespawn.Add(entity);
            }
        }

        public bool TryGetEntityByNetworkID(Guid NetworkID, out NetworkEntity entity)
        {
            return Entities.TryGetValue(NetworkID, out entity);
        }

        public void Tick()
        {
            Time++;

            while (Transport.TryReadPacket(out byte[] bytes))
            {
                using (MemoryStream stream = new MemoryStream(bytes))
                {
                    using (BinaryReader reader = new BinaryReader(stream))
                    {
                        RequestType requestType = (RequestType)reader.ReadByte();
                        if (RequestParsers.TryGetValue(requestType, out Action<BinaryReader> handler))
                        {
                            handler.Invoke(reader);
                        }
                        else
                        {
                            Console.WriteLine($"Unknown network request type: {requestType}");
                        }
                    }
                }
            }


            foreach (NetworkEntity entity in OwnedEntities.Values)
            {
                if (EntitiesToSpawn.Contains(entity) || EntitiesToDespawn.Contains(entity) || !entity.Serializer.Dirty)
                {
                    continue;
                }

                byte[] bytes = entity.Serializer.GetUpdateRequest(Time);

                if(bytes != null)
                {
                    Transport.SendPacket(bytes);
                }
            }

            foreach(NetworkEntity entity in Entities.Values)
            {
                List<byte[]> outgoingRPCs = entity.Serializer.GetOutgoingRPCs();
                foreach(byte[] bytes in outgoingRPCs)
                {
                    Transport.SendPacket(bytes);
                }
            }

            foreach (NetworkEntity entity in EntitiesToSpawn)
            {
                Transport.SendPacket(entity.Serializer.GetCreateRequest());
            }

            EntitiesToSpawn.Clear();

            foreach (NetworkEntity entity in EntitiesToDespawn)
            {
                Transport.SendPacket(entity.Serializer.GetDeleteRequest());
            }

            EntitiesToDespawn.Clear();

        }

        private void InitializeNetworkRequestParsers()
        {
            RequestParsers.Add(RequestType.CreateEntity, HandleCreateEntityRequest);
            RequestParsers.Add(RequestType.UpdateEntity, HandleUpdateEntityRequest);
            RequestParsers.Add(RequestType.DeleteEntity, HandleDeleteEntityRequest);
        }

        private void HandleCreateEntityRequest(BinaryReader reader)
        {
            Guid networkID = new Guid(reader.ReadBytes(16));
            string entityTypeName = reader.ReadString();
            Type entityType = null;

            if (Entities.TryGetValue(networkID, out NetworkEntity existingEntity))
            {
                if (!OwnedEntities.ContainsKey(existingEntity.NetworkID))
                {
                    existingEntity.Serializer.ReadNetworkProperties(reader);
                }
                return;
            }

            try
            {
                entityType = Type.GetType(entityTypeName);
            }
            catch (Exception) { }

            if (entityType != null && typeof(NetworkEntity).IsAssignableFrom(entityType))
            {
                NetworkEntity instance = (NetworkEntity)Activator.CreateInstance(entityType);
                instance.NetworkID = networkID;
                instance.Serializer.InitializeSerializationContext(SerializationContext);
                instance.Serializer.ReadNetworkProperties(reader);


                Entities.Add(networkID, instance);
                ProxyManager?.OnEntityCreated(instance);
            }
        }


        private void HandleUpdateEntityRequest(BinaryReader reader)
        {
            Guid networkID = new Guid(reader.ReadBytes(16));

            if (Entities.TryGetValue(networkID, out NetworkEntity entity))
            {
                if (OwnedEntities.ContainsKey(entity.NetworkID))
                {
                    return;
                }

                entity.Serializer.ReadNetworkProperties(reader);
            }
        }

        private void HandleDeleteEntityRequest(BinaryReader Reader)
        {
            Guid networkID = new Guid(Reader.ReadBytes(16));

            if(Entities.TryGetValue(networkID, out NetworkEntity entity))
            {
                ProxyManager?.OnEntityDeleted(entity);
            }

            Entities.Remove(networkID);
            OwnedEntities.Remove(networkID);
        }

        private void UpdateProxyManager(IProxyManager newProxyManager)
        {
            foreach (NetworkEntity Entity in Entities.Values)
            {
                ProxyManager?.OnEntityDeleted(Entity);
            }
            _ProxyManager = newProxyManager;
            foreach (NetworkEntity Entity in Entities.Values)
            {
                ProxyManager?.OnEntityCreated(Entity);
            }
        }

    }
}
