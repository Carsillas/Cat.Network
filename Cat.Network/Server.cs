using System;
using System.Collections.Generic;
using System.IO;

namespace Cat.Network
{
    public class Server
    {

        private Dictionary<RequestType, Action<BinaryReader>> RequestParsers { get; } = new Dictionary<RequestType, Action<BinaryReader>>();
        private SerializationContext SerializationContext { get; } = new SerializationContext
        {
            DeserializeDirtiesProperty = true
        };
        private List<ClientDetails> Clients { get; } = new List<ClientDetails>();
        public IEntityStorage EntityStorage { get; }

        private int Time { get; set; }

        public Server(IEntityStorage entityStorage)
        {
            InitializeNetworkRequestParsers();
            EntityStorage = entityStorage;
        }

        public void AddTransport(ITransport transport)
        {
            Clients.Add(new ClientDetails
            {
                Transport = transport
            });
        }
        private void InitializeNetworkRequestParsers()
        {
            RequestParsers.Add(RequestType.CreateEntity, HandleCreateEntityRequest);
            RequestParsers.Add(RequestType.UpdateEntity, HandleUpdateEntityRequest);
            RequestParsers.Add(RequestType.DeleteEntity, HandleDeleteEntityRequest);
            RequestParsers.Add(RequestType.RPC, HandleRPCRequest);
        }

        public void Tick()
        {
            Time++;

            foreach (ClientDetails client in Clients)
            {
                while (client.Transport.TryReadPacket(out byte[] request))
                {
                    using (MemoryStream stream = new MemoryStream(request))
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
            }

            HashSet<NetworkEntity> EntitiesToDelete = new HashSet<NetworkEntity>();
            HashSet<NetworkEntity> EntitiesToCreate = new HashSet<NetworkEntity>();

            foreach(ClientDetails client in Clients)
            {
                EntitiesToDelete.Clear();
                EntitiesToCreate.Clear();

                HashSet<NetworkEntity> RelevantEntities = EntityStorage.GetRelevantEntities(client.NetworkEntity);

                foreach(NetworkEntity entity in client.RelevantEntities)
                {
                    if (!RelevantEntities.Contains(entity))
                    {
                        EntitiesToDelete.Add(entity);
                    }
                }

                foreach(NetworkEntity entity in RelevantEntities)
                {
                    if (!client.RelevantEntities.Contains(entity))
                    {
                        EntitiesToCreate.Add(entity);
                    }
                }
                
                foreach(NetworkEntity entity in EntitiesToDelete)
                {
                    using (MemoryStream stream = new MemoryStream(256))
                    {
                        using (BinaryWriter writer = new BinaryWriter(stream))
                        {
                            writer.Write((byte)RequestType.DeleteEntity);
                            writer.Write(entity.NetworkID.ToByteArray());

                            client.Transport.SendPacket(stream.ToArray());
                        }
                    }
                    
                    client.RelevantEntities.Remove(entity);
                }

                foreach(NetworkEntity entity in client.RelevantEntities)
                {
                    byte[] bytes = entity.Serializer.GetUpdateRequest(Time);
                    if(bytes != null)
                    {
                        client.Transport.SendPacket(bytes);
                    }
                }

                foreach(NetworkEntity entity in EntitiesToCreate)
                {
                    client.Transport.SendPacket(entity.Serializer.GetCreateRequest());
                    client.RelevantEntities.Add(entity);
                }

            }

        }


        private void HandleCreateEntityRequest(BinaryReader reader)
        {
            Guid networkID = new Guid(reader.ReadBytes(16));
            string entityTypeName = reader.ReadString();
            Type entityType = null;

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
                EntityStorage.RegisterEntity(instance);
            }
        }


        private void HandleUpdateEntityRequest(BinaryReader reader)
        {
            Guid networkID = new Guid(reader.ReadBytes(16));

            if (EntityStorage.TryGetEntityByNetworkID(networkID, out NetworkEntity entity))
            {
                entity.Serializer.ReadNetworkProperties(reader);
            }
        }

        private void HandleDeleteEntityRequest(BinaryReader Reader)
        {
            Guid networkID = new Guid(Reader.ReadBytes(16));

            EntityStorage.UnregisterEntity(networkID);

        }


        private void HandleRPCRequest(BinaryReader reader)
        {
            Guid networkID = new Guid(reader.ReadBytes(16));
            
            if (EntityStorage.TryGetEntityByNetworkID(networkID, out NetworkEntity entity))
            {
                entity.Serializer.HandleIncomingRPCInvocation(reader);
            }
        }


        private class ClientDetails
        {
            public ITransport Transport { get; set; }
            public NetworkEntity NetworkEntity { get; set; }
            public HashSet<NetworkEntity> RelevantEntities { get; } = new HashSet<NetworkEntity>();

        }

    }
}
