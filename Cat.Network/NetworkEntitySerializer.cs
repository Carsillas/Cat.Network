using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Cat.Network
{
    internal sealed class NetworkEntitySerializer
    {
        internal enum SerializationOptions
        {
            All,
            Dirty
        }

        internal SerializationContext SerializationContext { get; private set; }

        private Dictionary<string, NetworkProperty> Properties { get; set; }
        private Dictionary<string, RPC> RPCs { get; set; }
        private NetworkEntity Entity { get; }

        private bool _Dirty;
        internal bool Dirty
        {
            get => _Dirty;
            set
            {
                _Dirty = value;
                CachedCreateRequest = null;
                CachedUpdateRequest = null;
            }
        }


        private int LastUpdateTime { get; set; }
        private byte[] CachedCreateRequest { get; set; }
        private byte[] CachedUpdateRequest { get; set; }
        private byte[] CachedDeleteRequest { get; set; }

        private List<byte[]> OutgoingRPCs { get; set; } = new List<byte[]>();

        internal NetworkEntitySerializer(NetworkEntity entity)
        {
            Entity = entity;

            Properties = GetNetworkProperties(Entity.GetType()).ToDictionary(
                property => $"{property.DeclaringType.Name}.{property.Name}",
                property => (NetworkProperty)property.GetValue(Entity));

            RPCs = GetRPCs(Entity.GetType()).ToDictionary(
                property => $"{property.DeclaringType.Name}.{property.Name}",
                property => (RPC)property.GetValue(Entity));

            foreach (NetworkProperty property in Properties.Values)
            {
                property.Entity = Entity;
            }

            foreach (KeyValuePair<string, RPC> rpc in RPCs)
            {
                rpc.Value.Entity = Entity;
                rpc.Value.ID = rpc.Key;
            }
        }

        internal void InitializeSerializationContext(SerializationContext context)
        {
            if(SerializationContext == null)
            {
                SerializationContext = context;
            } else if(SerializationContext != context)
            {
                throw new InvalidOperationException("Cannot spawn entity in multiple clients!");
            }

            foreach (NetworkProperty property in Properties.Values)
            {
                property.Initialize(context);
            }
        }

        internal byte[] GetDeleteRequest()
        {
            if(CachedDeleteRequest == null)
            {
                using (MemoryStream stream = new MemoryStream(24))
                {
                    using (BinaryWriter writer = new BinaryWriter(stream))
                    {
                        writer.Write((byte)RequestType.DeleteEntity);
                        writer.Write(Entity.NetworkID.ToByteArray());

                        CachedDeleteRequest = stream.ToArray();
                    }
                }
            }

            return CachedDeleteRequest;
        }

        internal byte[] GetCreateRequest()
        {
            if (CachedCreateRequest == null)
            {
                using (MemoryStream stream = new MemoryStream(256))
                {
                    using (BinaryWriter writer = new BinaryWriter(stream))
                    {
                        writer.Write((byte)RequestType.CreateEntity);
                        writer.Write(Entity.NetworkID.ToByteArray());
                        writer.Write(Entity.GetType().AssemblyQualifiedName);

                        WriteNetworkProperties(writer, SerializationOptions.All);

                        CachedCreateRequest = stream.ToArray();
                    }
                }
            }

            return CachedCreateRequest;
        }

        internal byte[] GetUpdateRequest(int time)
        {
            if (CachedUpdateRequest == null)
            {
                LastUpdateTime = time;
                using (MemoryStream stream = new MemoryStream(256))
                {
                    using (BinaryWriter writer = new BinaryWriter(stream))
                    {
                        writer.Write((byte)RequestType.UpdateEntity);
                        writer.Write(Entity.NetworkID.ToByteArray());

                        WriteNetworkProperties(writer, SerializationOptions.Dirty);

                        CachedUpdateRequest = stream.ToArray();
                    }
                }
            }

            if(time == LastUpdateTime)
            {
                return CachedUpdateRequest;
            }

            return null;
        }

        internal void HandleIncomingRPCInvocation(BinaryReader reader)
        {
            string rpcName = reader.ReadString();
            if (RPCs.TryGetValue(rpcName, out RPC rpc))
            {
                rpc.HandleIncomingInvocation(reader);
            }

        }

        internal void WriteOutgoingRPCInvocation(byte[] bytes)
        {
            OutgoingRPCs.Add(bytes);
        }

        internal List<byte[]> GetOutgoingRPCs()
        {
            List<byte[]> temp = OutgoingRPCs;
            OutgoingRPCs = new List<byte[]>();
            return temp;
        }

        internal void ReadNetworkProperties(BinaryReader reader)
        {
            uint propertyCount = reader.ReadUInt32();

            for (uint i = 0; i < propertyCount; i++)
            {
                string propertyName = reader.ReadString();
                Properties[propertyName].Deserialize(reader);
            }
        }

        private void WriteNetworkProperties(BinaryWriter writer, SerializationOptions serializationOptions)
        {

            uint propertyCount = 0;

            int propertyCountPosition = (int)writer.BaseStream.Position;
            writer.Seek(sizeof(uint), SeekOrigin.Current);

            foreach (var propertyPair in Properties)
            {
                switch (serializationOptions)
                {
                    case SerializationOptions.All:
                        Write();
                        break;
                    case SerializationOptions.Dirty:
                        if (propertyPair.Value.Dirty)
                        {
                            Write();
                        }
                        break;
                }

                void Write()
                {
                    writer.Write(propertyPair.Key);
                    propertyPair.Value.Serialize(writer);
                    propertyCount++;
                }

                propertyPair.Value.Dirty = false;
            }

            Entity.Serializer.Dirty = false;

            int endPosition = (int)writer.BaseStream.Position;

            writer.Seek(propertyCountPosition, SeekOrigin.Begin);
            writer.Write(propertyCount);
            writer.Seek(endPosition, SeekOrigin.Begin);

        }

        private static ConcurrentDictionary<Type, IReadOnlyList<PropertyInfo>> NetworkPropertiesCache { get; } = new ConcurrentDictionary<Type, IReadOnlyList<PropertyInfo>>();
        private static IReadOnlyList<PropertyInfo> GetNetworkProperties(Type type)
        {
            return NetworkPropertiesCache.GetOrAdd(type, ValueFactory);

            IReadOnlyList<PropertyInfo> ValueFactory(Type key)
            {

                Type currentType = key;
                IEnumerable<PropertyInfo> currentList = Enumerable.Empty<PropertyInfo>();
                while (currentType != null)
                {
                    currentList = currentList.Concat(currentType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
                    currentType = currentType.BaseType;
                }

                List<PropertyInfo> properties =
                    currentList.Where(X => typeof(NetworkProperty).IsAssignableFrom(X.PropertyType))
                    .OrderBy(X => X.Name)
                    .ToList();

                if (properties.Any(Property => Property.SetMethod != null || Property.CanWrite))
                {
                    string message = $"NetworkProperties must be get-only!\n" +
                        string.Join("\n", properties.Where(property => property.SetMethod != null || property.CanWrite).Select(property => $"{key.FullName} -> {property.DeclaringType.FullName}.{property.Name}"));
                    throw new AccessViolationException(message);
                }

                return properties;
            }
        }

        private static ConcurrentDictionary<Type, IReadOnlyList<PropertyInfo>> RPCsCache { get; } = new ConcurrentDictionary<Type, IReadOnlyList<PropertyInfo>>();
        private static IReadOnlyList<PropertyInfo> GetRPCs(Type type)
        {
            return RPCsCache.GetOrAdd(type, ValueFactory);

            IReadOnlyList<PropertyInfo> ValueFactory(Type key)
            {

                Type currentType = key;
                IEnumerable<PropertyInfo> currentList = Enumerable.Empty<PropertyInfo>();
                while (currentType != null)
                {
                    currentList = currentList.Concat(currentType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
                    currentType = currentType.BaseType;
                }

                List<PropertyInfo> properties =
                    currentList.Where(X => typeof(RPC).IsAssignableFrom(X.PropertyType))
                    .OrderBy(X => X.Name)
                    .ToList();

                if (properties.Any(Property => Property.SetMethod != null || Property.CanWrite))
                {
                    string message = $"RPCs must be get-only!\n" +
                        string.Join("\n", properties.Where(property => property.SetMethod != null || property.CanWrite).Select(property => $"{key.FullName} -> {property.DeclaringType.FullName}.{property.Name}"));
                    throw new AccessViolationException(message);
                }

                return properties;
            }
        }



    }
}
