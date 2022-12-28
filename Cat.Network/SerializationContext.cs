using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Cat.Network {

	public interface ISerializationContext {

		void RegisterSerializationFunction<T>(Action<BinaryWriter, T> serializeMethod, Func<BinaryReader, NetworkProperty<T>, T> deserializeMethod);

	}

	internal sealed class SerializationContext : ISerializationContext {

		private Dictionary<Type, (object Serializer, object Deserializer)> SerializationMethods { get; } = new Dictionary<Type, (object Serializer, object Deserializer)>();

		internal bool DeserializeDirtiesProperty { get; set; }

		public SerializationContext() {

			RegisterSerializationFunction<byte>(SerializeByte, DeserializeByte);
			RegisterSerializationFunction<short>(SerializeShort, DeserializeShort);
			RegisterSerializationFunction<int>(SerializeInt, DeserializeInt);
			RegisterSerializationFunction<long>(SerializeLong, DeserializeLong);
			RegisterSerializationFunction<ulong>(SerializeULong, DeserializeULong);
			RegisterSerializationFunction<float>(SerializeFloat, DeserializeFloat);
			RegisterSerializationFunction<string>(SerializeString, DeserializeString);
			RegisterSerializationFunction<bool>(SerializeBool, DeserializeBool);

		}

		public void RegisterSerializationFunction<T>(Action<BinaryWriter, T> serializeMethod, Func<BinaryReader, NetworkProperty<T>, T> deserializeMethod) {
			SerializationMethods.Add(typeof(T), (serializeMethod, deserializeMethod));
		}

		public Action<BinaryWriter, T> GetSerializationFunction<T>() {

			Type serializedType = typeof(T);

			if (typeof(T).IsEnum) {
				serializedType = Enum.GetUnderlyingType(serializedType);
			} else if (typeof(NetworkEntity).IsAssignableFrom(typeof(T))) {
				serializedType = typeof(NetworkEntity);
			}

			if (SerializationMethods.TryGetValue(serializedType, out (object Serializer, object Deserializer) methods)) {
				return Unsafe.As<Action<BinaryWriter, T>>(methods.Serializer);
			}
			throw new Exception($"Unsupported type encountered in {nameof(NetworkProperty)}: {typeof(T)}");
		}

		public Func<BinaryReader, NetworkProperty<T>, T> GetDeserializationFunction<T>() {

			Type serializedType = typeof(T);

			if (typeof(T).IsEnum) {
				serializedType = Enum.GetUnderlyingType(serializedType);
			} else if (typeof(NetworkEntity).IsAssignableFrom(typeof(T))) {
				serializedType = typeof(NetworkEntity);
			}

			if (SerializationMethods.TryGetValue(serializedType, out (object Serializer, object Deserializer) methods)) {
				return Unsafe.As<Func<BinaryReader, NetworkProperty<T>, T>>(methods.Deserializer);
			}
			throw new Exception($"Unsupported type encountered in {nameof(NetworkProperty)}: {typeof(T)}");
		}


		private static void SerializeByte(BinaryWriter writer, byte value) {
			writer.Write(value);
		}
		private static byte DeserializeByte(BinaryReader reader, NetworkProperty<byte> NetworkProperty) {
			return reader.ReadByte();
		}
		private static void SerializeShort(BinaryWriter writer, short value) {
			writer.Write(value);
		}
		private static short DeserializeShort(BinaryReader reader, NetworkProperty<short> NetworkProperty) {
			return reader.ReadInt16();
		}
		private static void SerializeInt(BinaryWriter writer, int value) {
			writer.Write(value);
		}
		private static int DeserializeInt(BinaryReader reader, NetworkProperty<int> NetworkProperty) {
			return reader.ReadInt32();
		}
		private static void SerializeLong(BinaryWriter writer, long value) {
			writer.Write(value);
		}
		private static long DeserializeLong(BinaryReader reader, NetworkProperty<long> NetworkProperty) {
			return reader.ReadInt64();
		}
		private static void SerializeULong(BinaryWriter writer, ulong value) {
			writer.Write(value);
		}
		private static ulong DeserializeULong(BinaryReader reader, NetworkProperty<ulong> NetworkProperty) {
			return reader.ReadUInt64();
		}
		private static void SerializeFloat(BinaryWriter writer, float value) {
			writer.Write(value);
		}
		private static float DeserializeFloat(BinaryReader reader, NetworkProperty<float> NetworkProperty) {
			return reader.ReadSingle();
		}
		private static void SerializeBool(BinaryWriter writer, bool value) {
			writer.Write(value);
		}
		private static bool DeserializeBool(BinaryReader reader, NetworkProperty<bool> NetworkProperty) {
			return reader.ReadBoolean();
		}
		private static void SerializeString(BinaryWriter writer, string value) {
			bool hasValue = value != null;
			writer.Write(hasValue);
			if (hasValue) {
				writer.Write(value);
			}
		}
		private static string DeserializeString(BinaryReader reader, NetworkProperty<string> NetworkProperty) {
			return reader.ReadBoolean() ? reader.ReadString() : null;
		}



	}
}
