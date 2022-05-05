using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cat.Network {
	internal sealed class SerializationContext {
		private Dictionary<Type, (object Serializer, object Deserializer)> SerializationMethods { get; } = new Dictionary<Type, (object Serializer, object Deserializer)>();

		internal bool DeserializeDirtiesProperty { get; set; }

		public SerializationContext() {
			AddType<int>(SerializeInt, DeserializeInt);
			AddType<ulong>(SerializeULong, DeserializeULong);
			AddType<float>(SerializeFloat, DeserializeFloat);
			AddType<string>(SerializeString, DeserializeString);
			AddType<bool>(SerializeBool, DeserializeBool);

		}

		internal void AddType<T>(Action<BinaryWriter, T> serializeMethod, Func<BinaryReader, NetworkProperty<T>, T> deserializeMethod) where T : IEquatable<T> {
			SerializationMethods.Add(typeof(T), (serializeMethod, deserializeMethod));
		}

		public Action<BinaryWriter, T> GetSerializationFunction<T>() {
			if (SerializationMethods.TryGetValue(typeof(T), out (object Serializer, object Deserializer) methods)) {
				return (Action<BinaryWriter, T>)(methods.Serializer);
			}
			throw new Exception($"Unsupported type encountered in {nameof(NetworkProperty)}: {typeof(T)}");
		}

		public Func<BinaryReader, NetworkProperty<T>, T> GetDeserializationFunction<T>() where T : IEquatable<T> {
			if (SerializationMethods.TryGetValue(typeof(T), out (object Serializer, object Deserializer) methods)) {
				return (Func<BinaryReader, NetworkProperty<T>, T>)methods.Deserializer;
			}
			throw new Exception($"Unsupported type encountered in {nameof(NetworkProperty)}: {typeof(T)}");
		}


		private static void SerializeInt(BinaryWriter writer, int Value) {
			writer.Write(Value);
		}
		private static int DeserializeInt(BinaryReader reader, NetworkProperty<int> NetworkProperty) {
			return reader.ReadInt32();
		}
		private static void SerializeULong(BinaryWriter writer, ulong Value) {
			writer.Write(Value);
		}
		private static ulong DeserializeULong(BinaryReader reader, NetworkProperty<ulong> NetworkProperty) {
			return reader.ReadUInt64();
		}
		private static void SerializeFloat(BinaryWriter writer, float Value) {
			writer.Write(Value);
		}
		private static float DeserializeFloat(BinaryReader reader, NetworkProperty<float> NetworkProperty) {
			return reader.ReadSingle();
		}
		private static void SerializeBool(BinaryWriter writer, bool Value) {
			writer.Write(Value);
		}
		private static bool DeserializeBool(BinaryReader reader, NetworkProperty<bool> NetworkProperty) {
			return reader.ReadBoolean();
		}
		private static void SerializeString(BinaryWriter writer, string Value) {
			bool hasValue = Value != null;
			writer.Write(hasValue);
			if (hasValue) {
				writer.Write(Value);
			}
		}
		private static string DeserializeString(BinaryReader reader, NetworkProperty<string> NetworkProperty) {
			return reader.ReadBoolean() ? reader.ReadString() : null;
		}

	}
}
