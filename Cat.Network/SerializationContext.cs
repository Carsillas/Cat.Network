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


		private static void SerializeInt(BinaryWriter Writer, int Value) {
			Writer.Write(Value);
		}
		private static int DeserializeInt(BinaryReader Reader, NetworkProperty<int> NetworkProperty) {
			return Reader.ReadInt32();
		}
		private static void SerializeFloat(BinaryWriter Writer, float Value) {
			Writer.Write(Value);
		}
		private static float DeserializeFloat(BinaryReader Reader, NetworkProperty<float> NetworkProperty) {
			return Reader.ReadSingle();
		}
		private static void SerializeBool(BinaryWriter Writer, bool Value) {
			Writer.Write(Value);
		}
		private static bool DeserializeBool(BinaryReader Reader, NetworkProperty<bool> NetworkProperty) {
			return Reader.ReadBoolean();
		}
		private static void SerializeString(BinaryWriter Writer, string Value) {
			bool hasValue = Value != null;
			Writer.Write(hasValue);
			if (hasValue) {
				Writer.Write(Value);
			}
		}
		private static string DeserializeString(BinaryReader Reader, NetworkProperty<string> NetworkProperty) {
			return Reader.ReadBoolean() ? Reader.ReadString() : null;
		}

	}
}
