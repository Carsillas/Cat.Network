using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Cat.Network {
	internal sealed class NetworkEntitySerializer {

		private static HashAlgorithm HashAlgorithm { get; } = MD5.Create();

		internal SerializationContext SerializationContext { get; private set; }

		private Dictionary<string, NetworkProperty> Properties { get; set; }

		private class MulticastInfo {
			public MethodInfo Method { get; set; }
			public Multicast Metadata { get;set;}
		}

		private IReadOnlyDictionary<Guid, MethodInfo> RPCs { get; set; }
		private IReadOnlyDictionary<Guid, MulticastInfo> Multicasts { get; set; }

		private NetworkEntity Entity { get; }

		private bool _CreateDirty;
		private bool _UpdateDirty;
		internal bool CreateDirty {
			get => _CreateDirty;
			set {
				_CreateDirty = value;
				CachedCreateRequest = null;
			}
		}
		internal bool UpdateDirty {
			get => _UpdateDirty;
			set {
				_UpdateDirty = value;
				CachedUpdateRequest = null;
			}
		}


		private int LastUpdateTime { get; set; }
		private byte[] CachedCreateRequest { get; set; }
		private byte[] CachedUpdateRequest { get; set; }
		private byte[] CachedDeleteRequest { get; set; }

		private List<byte[]> OutgoingRPCs { get; set; } = new List<byte[]>();

		internal NetworkEntitySerializer(NetworkEntity entity) {
			Entity = entity;

			Properties = GetNetworkProperties(Entity.GetType()).ToDictionary(
				property => $"{property.DeclaringType.Name}.{property.Name}",
				property => (NetworkProperty)property.GetValue(Entity));

			RPCs = GetRPCs(Entity.GetType());
			Multicasts = GetMulticasts(Entity.GetType());

			foreach (NetworkProperty property in Properties.Values) {
				property.Entity = Entity;
			}
		}

		internal void InitializeSerializationContext(SerializationContext context) {
			if (SerializationContext == null) {
				SerializationContext = context;
			} else if (SerializationContext != context) {
				throw new InvalidOperationException("Cannot spawn entity in multiple clients!");
			}

			foreach (NetworkProperty property in Properties.Values) {
				property.Initialize(context);
			}
		}

		internal byte[] GetDeleteRequest() {
			if (CachedDeleteRequest == null) {
				using (MemoryStream stream = new MemoryStream(24)) {
					using (BinaryWriter writer = new BinaryWriter(stream)) {
						writer.Write((byte)RequestType.DeleteEntity);
						writer.Write(Entity.NetworkID.ToByteArray());

						CachedDeleteRequest = stream.ToArray();
					}
				}
			}

			return CachedDeleteRequest;
		}

		internal byte[] GetCreateRequest() {
			if (CachedCreateRequest == null) {
				using (MemoryStream stream = new MemoryStream(256)) {
					using (BinaryWriter writer = new BinaryWriter(stream)) {
						writer.Write((byte)RequestType.CreateEntity);
						writer.Write(Entity.NetworkID.ToByteArray());
						writer.Write(Entity.GetType().AssemblyQualifiedName);

						WriteNetworkProperties(writer, NetworkPropertySerializeTrigger.Creation);

						CachedCreateRequest = stream.ToArray();
					}
				}
			}

			return CachedCreateRequest;
		}

		internal byte[] GetUpdateRequest(int time) {
			if (CachedUpdateRequest == null) {
				LastUpdateTime = time;
				using (MemoryStream stream = new MemoryStream(256)) {
					using (BinaryWriter writer = new BinaryWriter(stream)) {
						writer.Write((byte)RequestType.UpdateEntity);
						writer.Write(Entity.NetworkID.ToByteArray());

						WriteNetworkProperties(writer, NetworkPropertySerializeTrigger.Modification);

						CachedUpdateRequest = stream.ToArray();
					}
				}
			}

			if (time == LastUpdateTime) {
				return CachedUpdateRequest;
			}

			return null;
		}

		internal void WriteRPCID(BinaryWriter writer, MethodInfo methodInfo) {
			writer.Write(RPCs.Where(kvp => kvp.Value == methodInfo).First().Key.ToByteArray());
		}

		internal void WriteMulticastID(BinaryWriter writer, MethodInfo methodInfo) {
			writer.Write(Multicasts.Where(kvp => kvp.Value.Method == methodInfo).First().Key.ToByteArray());
		}

		internal void HandleIncomingRPCInvocation(BinaryReader reader) {

			Guid rpcID = new Guid(reader.ReadBytes(16));
			if (RPCs.TryGetValue(rpcID, out MethodInfo rpc)) {
				ParameterInfo[] Parameters = rpc.GetParameters();

				MethodInfo method = typeof(NetworkEntity).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
					.First(m => m.Name == $"DeserializeInvokeAction{Parameters.Length}");

				if (Parameters.Length == 0) {
					method.Invoke(Entity, new object[] { reader, rpc });
				} else {
					method.GetGenericMethodDefinition()
						.MakeGenericMethod(Parameters.Select(Parameter => Parameter.ParameterType).ToArray())
						.Invoke(Entity, new object[] { reader, rpc });
				}
			}

		}
		internal void HandleIncomingMulticastInvocation(BinaryReader reader, bool requireServerPermission) {

			Guid multicastID = new Guid(reader.ReadBytes(16));
			if (Multicasts.TryGetValue(multicastID, out MulticastInfo multicast) && (multicast.Metadata.ExecuteOnServer || !requireServerPermission)) {
				ParameterInfo[] Parameters = multicast.Method.GetParameters();

				MethodInfo method = typeof(NetworkEntity).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
					.First(m => m.Name == $"DeserializeInvokeAction{Parameters.Length}");

				if (Parameters.Length == 0) {
					method.Invoke(Entity, new object[] { reader, multicast.Method });
				} else {
					method.GetGenericMethodDefinition()
						.MakeGenericMethod(Parameters.Select(Parameter => Parameter.ParameterType).ToArray())
						.Invoke(Entity, new object[] { reader, multicast.Method });
				}
			}

		}

		internal void WriteOutgoingRPCInvocation(byte[] bytes) {
			OutgoingRPCs.Add(bytes);
		}

		internal List<byte[]> GetOutgoingRPCs() {
			List<byte[]> temp = OutgoingRPCs;
			OutgoingRPCs = new List<byte[]>();
			return temp;
		}

		internal void ReadNetworkProperties(BinaryReader reader) {
			uint propertyCount = reader.ReadUInt32();

			for (uint i = 0; i < propertyCount; i++) {
				string propertyName = reader.ReadString();
				Properties[propertyName].Deserialize(reader);
			}
		}

		private void WriteNetworkProperties(BinaryWriter writer, NetworkPropertySerializeTrigger triggers) {

			uint propertyCount = 0;

			int propertyCountPosition = (int)writer.BaseStream.Position;
			writer.Seek(sizeof(uint), SeekOrigin.Current);

			foreach (var propertyPair in Properties) {

				bool creationFlag = triggers.HasFlag(NetworkPropertySerializeTrigger.Creation) && propertyPair.Value.Triggers.HasFlag(NetworkPropertySerializeTrigger.Creation);
				bool modifyFlag = triggers.HasFlag(NetworkPropertySerializeTrigger.Modification) &&	propertyPair.Value.Triggers.HasFlag(NetworkPropertySerializeTrigger.Modification);

				if (creationFlag) {
					Write();
				} else if (propertyPair.Value.Dirty && modifyFlag) {
					Write();
					propertyPair.Value.Dirty = false;
				}

				void Write() {
					writer.Write(propertyPair.Key);
					propertyPair.Value.Serialize(writer);
					propertyCount++;
				}

			}

			if (triggers.HasFlag(NetworkPropertySerializeTrigger.Modification)) {
				Entity.Serializer.UpdateDirty = false;
			}

			int endPosition = (int)writer.BaseStream.Position;

			writer.Seek(propertyCountPosition, SeekOrigin.Begin);
			writer.Write(propertyCount);
			writer.Seek(endPosition, SeekOrigin.Begin);

		}

		private static ConcurrentDictionary<Type, IReadOnlyList<PropertyInfo>> NetworkPropertiesCache { get; } = new ConcurrentDictionary<Type, IReadOnlyList<PropertyInfo>>();
		private static IReadOnlyList<PropertyInfo> GetNetworkProperties(Type type) {
			return NetworkPropertiesCache.GetOrAdd(type, ValueFactory);

			IReadOnlyList<PropertyInfo> ValueFactory(Type key) {

				Type currentType = key;
				IEnumerable<PropertyInfo> currentList = Enumerable.Empty<PropertyInfo>();
				while (currentType != null) {
					currentList = currentList.Concat(currentType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
					currentType = currentType.BaseType;
				}

				List<PropertyInfo> properties =
					currentList.Where(X => typeof(NetworkProperty).IsAssignableFrom(X.PropertyType))
					.OrderBy(X => X.Name)
					.ToList();

				if (properties.Any(Property => Property.SetMethod != null || Property.CanWrite)) {
					string message = $"NetworkProperties must be get-only!\n" +
						string.Join("\n", properties.Where(property => property.SetMethod != null || property.CanWrite).Select(property => $"{key.FullName} -> {property.DeclaringType.FullName}.{property.Name}"));
					throw new AccessViolationException(message);
				}

				return properties;
			}
		}

		private static ConcurrentDictionary<Type, IReadOnlyDictionary<Guid, MethodInfo>> RPCsCache { get; } =
			new ConcurrentDictionary<Type, IReadOnlyDictionary<Guid, MethodInfo>>();
		private static ConcurrentDictionary<Type, IReadOnlyDictionary<Guid, MulticastInfo>> MulticastsCache { get; } =
			new ConcurrentDictionary<Type, IReadOnlyDictionary<Guid, MulticastInfo>>();
		private static IReadOnlyDictionary<Guid, MethodInfo> GetRPCs(Type type) {
			return RPCsCache.GetOrAdd(type, ValueFactory);

			IReadOnlyDictionary<Guid, MethodInfo> ValueFactory(Type key) {

				Type currentType = key;
				IEnumerable<MethodInfo> currentList = Enumerable.Empty<MethodInfo>();
				while (currentType != null) {
					currentList = currentList.Concat(currentType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
					currentType = currentType.BaseType;
				}

				List<MethodInfo> methods =
					currentList.Where(X => X.IsDefined(typeof(RPC)))
					.ToList();

				return methods.ToDictionary(
				methodInfo => GetStringHash(methodInfo.ToString()),
				methodInfo => methodInfo);

				Guid GetStringHash(string value) {
					byte[] stringBytes = Encoding.UTF8.GetBytes(value);
					byte[] hash = HashAlgorithm.ComputeHash(stringBytes);
					return new Guid(hash);
				}
			}
		}

		private static IReadOnlyDictionary<Guid, MulticastInfo> GetMulticasts(Type type) {
			return MulticastsCache.GetOrAdd(type, ValueFactory);

			IReadOnlyDictionary<Guid, MulticastInfo> ValueFactory(Type key) {

				Type currentType = key;
				IEnumerable<MethodInfo> currentList = Enumerable.Empty<MethodInfo>();
				while (currentType != null) {
					currentList = currentList.Concat(currentType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
					currentType = currentType.BaseType;
				}

				List<MethodInfo> methods =
					currentList.Where(X => X.IsDefined(typeof(Multicast)))
					.ToList();

				return methods.ToDictionary(
				methodInfo => GetStringHash(methodInfo.ToString()),
				methodInfo => new MulticastInfo { Method = methodInfo, Metadata = methodInfo.GetCustomAttribute<Multicast>() });

				Guid GetStringHash(string value) {
					byte[] stringBytes = Encoding.UTF8.GetBytes(value);
					byte[] hash = HashAlgorithm.ComputeHash(stringBytes);
					return new Guid(hash);
				}
			}
		}
	}
}
