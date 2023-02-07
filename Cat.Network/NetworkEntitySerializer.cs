using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using static Cat.Network.ReflectionUtils;

namespace Cat.Network {

	internal sealed class NetworkEntitySerializer {

		internal SerializationContext SerializationContext { get; private set; }

		private IReadOnlyDictionary<string, NetworkProperty> Properties { get; set; }
		private IReadOnlyDictionary<Guid, MethodInfo> RPCs { get; set; }
		private IReadOnlyDictionary<Guid, MulticastInfo> Multicasts { get; set; }


		private static Dictionary<string, byte[]> PreserializedPropertyNames { get; } = new Dictionary<string, byte[]>();

		private MemoryStream CreateStream { get; } = new MemoryStream(256);
		private MemoryStream UpdateStream { get; } = new MemoryStream(256);

		private BinaryWriter CreateWriter { get; }
		private BinaryWriter UpdateWriter { get; }



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
		private RequestBuffer? CachedCreateRequest { get; set; }
		private RequestBuffer? CachedUpdateRequest { get; set; }
		private RequestBuffer? CachedDeleteRequest { get; set; }


		private List<byte[]> OutgoingRPCs { get; set; } = new List<byte[]>();

		internal NetworkEntitySerializer(NetworkEntity entity) {
			Entity = entity;

			CreateWriter = new BinaryWriter(CreateStream);
			UpdateWriter = new BinaryWriter(UpdateStream);

			Properties = GetNetworkProperties(Entity);

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

		internal RequestBuffer GetDeleteRequest() {
			if (CachedDeleteRequest == null) {
				using (MemoryStream stream = new MemoryStream(24)) {
					using (BinaryWriter writer = new BinaryWriter(stream)) {
						writer.Write((byte)RequestType.DeleteEntity);
						writer.Write(Entity.NetworkID.ToByteArray());

						CachedDeleteRequest = new RequestBuffer { Buffer = stream.ToArray(), ByteCount = (int)stream.Length };
					}
				}
			}

			return CachedDeleteRequest.Value;
		}

		internal RequestBuffer GetCreateRequest() {
			if (CachedCreateRequest == null) {
				CreateWriter.BaseStream.Seek(0, SeekOrigin.Begin);

				CreateWriter.Write((byte)RequestType.CreateEntity);
				CreateWriter.Write(Entity.NetworkID.ToByteArray());
				CreateWriter.Write(Entity.GetType().AssemblyQualifiedName);

				WriteNetworkProperties(CreateWriter, NetworkPropertySerializeTrigger.Creation);

				CachedCreateRequest = new RequestBuffer {
					Buffer = CreateStream.GetBuffer(),
					ByteCount = (int)CreateStream.Length
				};
			}

			return CachedCreateRequest.Value;
		}

		internal RequestBuffer? GetUpdateRequest(int time) {
			if (CachedUpdateRequest == null) {
				LastUpdateTime = time;

				UpdateWriter.BaseStream.Seek(0, SeekOrigin.Begin);

				UpdateWriter.Write((byte)RequestType.UpdateEntity);
				UpdateWriter.Write(Entity.NetworkID.ToByteArray());

				WriteNetworkProperties(UpdateWriter, NetworkPropertySerializeTrigger.Modification);

				CachedUpdateRequest = new RequestBuffer {
					Buffer = UpdateStream.GetBuffer(),
					ByteCount = (int)UpdateStream.Length
				};

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


		private byte[] GetSerializedPropertyName(string name, NetworkProperty property) {
			if(property.PropertyNameBytes == null) {

				if(!PreserializedPropertyNames.TryGetValue(name, out byte[] serialized)) {
					using (MemoryStream stream = new MemoryStream(name.Length * 2)) {
						using (BinaryWriter writer = new BinaryWriter(stream)) {
							writer.Write(name);

							serialized = stream.ToArray();
							PreserializedPropertyNames.Add(name, serialized);
						}
					}
				}

				property.PropertyNameBytes = serialized;

			}

			return property.PropertyNameBytes;
		}

		private void WriteNetworkProperties(BinaryWriter writer, NetworkPropertySerializeTrigger triggers) {

			uint propertyCount = 0;

			int propertyCountPosition = (int)writer.BaseStream.Position;
			writer.Seek(sizeof(uint), SeekOrigin.Current);

			foreach (var propertyPair in Properties) {

				bool creationFlag = triggers.HasFlag(NetworkPropertySerializeTrigger.Creation) && propertyPair.Value.Triggers.HasFlag(NetworkPropertySerializeTrigger.Creation);
				bool modifyFlag = triggers.HasFlag(NetworkPropertySerializeTrigger.Modification) && propertyPair.Value.Triggers.HasFlag(NetworkPropertySerializeTrigger.Modification);

				if (creationFlag) {
					Write();
				} else if (propertyPair.Value.Dirty && modifyFlag) {
					Write();
					propertyPair.Value.Dirty = false;
				}

				void Write() {
					writer.Write(GetSerializedPropertyName(propertyPair.Key, propertyPair.Value));
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


	}
}
