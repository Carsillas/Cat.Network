using System.Buffers.Binary;
using System.Reflection;

namespace Cat.Network;

internal static class NetworkObjectUpgradeCodec {
	public static void Serialize<T>(BufferWriter writer, T value, SerializationContext context) {
		SerializeValue(writer, typeof(T), value, context, stringLengthPrefixed: false, networkObjectAsProperty: true);
	}

	public static T Deserialize<T>(ReadOnlySpan<byte> data, SerializationContext context) {
		int offset = 0;
		object? value = DeserializeValue(typeof(T), data, ref offset, context, stringLengthPrefixed: false, networkObjectAsProperty: true);
		if (offset != data.Length) {
			throw new InvalidOperationException("Upgrade field payload was not fully consumed.");
		}

		return (T)value!;
	}

	private static void SerializeValue(BufferWriter writer, Type type, object? value, SerializationContext context, bool stringLengthPrefixed, bool networkObjectAsProperty) {
		Type? nullableUnderlyingType = Nullable.GetUnderlyingType(type);
		if (nullableUnderlyingType is not null) {
			if (value is null) {
				WriteByte(writer, 0);
				return;
			}

			WriteByte(writer, 1);
			SerializeValue(writer, nullableUnderlyingType, value, context, stringLengthPrefixed, networkObjectAsProperty);
			return;
		}

		if (type == typeof(bool)) {
			WriteByte(writer, (bool)value! ? (byte)1 : (byte)0);
		} else if (type == typeof(byte)) {
			WriteByte(writer, (byte)value!);
		} else if (type == typeof(sbyte)) {
			WriteByte(writer, unchecked((byte)(sbyte)value!));
		} else if (type == typeof(short)) {
			WriteInt16(writer, (short)value!);
		} else if (type == typeof(ushort)) {
			WriteUInt16(writer, (ushort)value!);
		} else if (type == typeof(int)) {
			WriteInt32(writer, (int)value!);
		} else if (type == typeof(uint)) {
			WriteUInt32(writer, (uint)value!);
		} else if (type == typeof(long)) {
			WriteInt64(writer, (long)value!);
		} else if (type == typeof(ulong)) {
			WriteUInt64(writer, (ulong)value!);
		} else if (type == typeof(float)) {
			WriteSingle(writer, (float)value!);
		} else if (type == typeof(double)) {
			WriteDouble(writer, (double)value!);
		} else if (type == typeof(string)) {
			string stringValue = (string?)value ?? throw new InvalidOperationException("String upgrade fields cannot be null.");
			if (stringLengthPrefixed) {
				byte[] stringData = System.Text.Encoding.UTF8.GetBytes(stringValue);
				WriteUInt32(writer, (uint)stringData.Length);
				WriteBytes(writer, stringData);
			} else {
				WriteUtf8(writer, stringValue);
			}
		} else if (type == typeof(Guid)) {
			WriteGuid(writer, (Guid)value!);
		} else if (typeof(NetworkObject).IsAssignableFrom(type)) {
			SerializeNetworkObject(writer, value, context, networkObjectAsProperty);
		} else if (type.IsValueType && IsSupportedStructType(type)) {
			foreach (FieldInfo field in GetSerializableStructFields(type)) {
				SerializeValue(writer, field.FieldType, field.GetValue(value!), context, stringLengthPrefixed: true, networkObjectAsProperty: false);
			}
		} else {
			throw new InvalidOperationException($"Upgrade field type '{type.FullName}' is not supported.");
		}
	}

	private static object? DeserializeValue(Type type, ReadOnlySpan<byte> data, ref int offset, SerializationContext context, bool stringLengthPrefixed, bool networkObjectAsProperty) {
		Type? nullableUnderlyingType = Nullable.GetUnderlyingType(type);
		if (nullableUnderlyingType is not null) {
			byte hasValue = ReadByte(data, ref offset);
			return hasValue switch {
				0 => null,
				1 => DeserializeValue(nullableUnderlyingType, data, ref offset, context, stringLengthPrefixed, networkObjectAsProperty),
				_ => throw new InvalidOperationException("Upgrade field nullable flag is invalid.")
			};
		}

		if (type == typeof(bool)) {
			return ReadByte(data, ref offset) != 0;
		}
		if (type == typeof(byte)) {
			return ReadByte(data, ref offset);
		}
		if (type == typeof(sbyte)) {
			return unchecked((sbyte)ReadByte(data, ref offset));
		}
		if (type == typeof(short)) {
			return ReadInt16(data, ref offset);
		}
		if (type == typeof(ushort)) {
			return ReadUInt16(data, ref offset);
		}
		if (type == typeof(int)) {
			return ReadInt32(data, ref offset);
		}
		if (type == typeof(uint)) {
			return ReadUInt32(data, ref offset);
		}
		if (type == typeof(long)) {
			return ReadInt64(data, ref offset);
		}
		if (type == typeof(ulong)) {
			return ReadUInt64(data, ref offset);
		}
		if (type == typeof(float)) {
			return ReadSingle(data, ref offset);
		}
		if (type == typeof(double)) {
			return ReadDouble(data, ref offset);
		}
		if (type == typeof(string)) {
			if (!stringLengthPrefixed) {
				string value = System.Text.Encoding.UTF8.GetString(data[offset..]);
				offset = data.Length;
				return value;
			}

			uint stringByteCount = ReadUInt32(data, ref offset);
			if (data.Length - offset < stringByteCount) {
				throw new InvalidOperationException("Upgrade field string payload is truncated.");
			}

			string lengthPrefixedValue = System.Text.Encoding.UTF8.GetString(data.Slice(offset, (int)stringByteCount));
			offset += (int)stringByteCount;
			return lengthPrefixedValue;
		}
		if (type == typeof(Guid)) {
			return ReadGuid(data, ref offset);
		}
		if (typeof(NetworkObject).IsAssignableFrom(type)) {
			return DeserializeNetworkObject(type, data, ref offset, context, networkObjectAsProperty);
		}
		if (type.IsValueType && IsSupportedStructType(type)) {
			object boxed = Activator.CreateInstance(type)!;
			foreach (FieldInfo field in GetSerializableStructFields(type)) {
				object? fieldValue = DeserializeValue(field.FieldType, data, ref offset, context, stringLengthPrefixed: true, networkObjectAsProperty: false);
				field.SetValue(boxed, fieldValue);
			}

			return boxed;
		}

		throw new InvalidOperationException($"Upgrade field type '{type.FullName}' is not supported.");
	}

	private static void SerializeNetworkObject(BufferWriter writer, object? value, SerializationContext context, bool networkObjectAsProperty) {
		if (networkObjectAsProperty) {
			if (value is null) {
				WriteByte(writer, (byte)NetworkObjectUpdateMode.Clear);
				return;
			}

			NetworkObject networkObject = (NetworkObject)value;
			if (!context.TypeCatalogue.TryFindSerializer(networkObject.GetType(), out INetworkObjectSerializer? serializer)) {
				throw new InvalidOperationException($"Serializer for type '{networkObject.GetType().FullName}' is not registered.");
			}

			WriteByte(writer, (byte)NetworkObjectUpdateMode.Replace);
			WriteGuid(writer, GetNetworkObjectTypeId(networkObject.GetType()));
			serializer.Serialize(writer, networkObject, context, new SerializationOptions(MemberSelectionMode.All, MemberIdentificationMode.Name));
			return;
		}

		throw new InvalidOperationException("NetworkObject values are not supported inside upgrade struct fields.");
	}

	private static object? DeserializeNetworkObject(Type type, ReadOnlySpan<byte> data, ref int offset, SerializationContext context, bool networkObjectAsProperty) {
		if (!networkObjectAsProperty) {
			throw new InvalidOperationException("NetworkObject values are not supported inside upgrade struct fields.");
		}

		NetworkObjectUpdateMode updateMode = (NetworkObjectUpdateMode)ReadByte(data, ref offset);
		switch (updateMode) {
			case NetworkObjectUpdateMode.Clear:
				return null;
			case NetworkObjectUpdateMode.Replace:
				Guid typeId = ReadGuid(data, ref offset);
				if (!context.TypeCatalogue.TryFindType(typeId, out Type? runtimeType)) {
					throw new InvalidOperationException($"Type id '{typeId}' is not registered.");
				}
				if (!type.IsAssignableFrom(runtimeType)) {
					throw new InvalidOperationException($"Type '{runtimeType.FullName}' is not assignable to '{type.FullName}'.");
				}
				if (!context.TypeCatalogue.TryFindSerializer(runtimeType, out INetworkObjectSerializer? serializer)) {
					throw new InvalidOperationException($"Serializer for type '{runtimeType.FullName}' is not registered.");
				}
				if (Activator.CreateInstance(runtimeType) is not NetworkObject target) {
					throw new InvalidOperationException($"Type '{runtimeType.FullName}' could not be constructed.");
				}

				serializer.Deserialize(target, data[offset..], context);
				offset = data.Length;
				return target;
			default:
				throw new InvalidOperationException($"Upgrade field object update mode '{updateMode}' is not supported.");
		}
	}

	private static IEnumerable<FieldInfo> GetSerializableStructFields(Type type) {
		return type.GetFields(BindingFlags.Instance | BindingFlags.Public)
			.Where(static field => !field.IsStatic)
			.OrderBy(static field => field.Name, StringComparer.Ordinal);
	}

	private static bool IsSupportedStructType(Type type) {
		return IsSupportedStructType(type, new HashSet<Type>());
	}

	private static bool IsSupportedStructType(Type type, HashSet<Type> visitedTypes) {
		if (!visitedTypes.Add(type)) {
			return false;
		}

		try {
			FieldInfo[] fields = GetSerializableStructFields(type).ToArray();
			return fields.Length > 0 && fields.All(field => IsSupportedStructFieldType(field.FieldType, visitedTypes));
		} finally {
			visitedTypes.Remove(type);
		}
	}

	private static bool IsSupportedStructFieldType(Type type, HashSet<Type> visitedTypes) {
		Type? nullableUnderlyingType = Nullable.GetUnderlyingType(type);
		if (nullableUnderlyingType is not null) {
			return IsSupportedStructFieldType(nullableUnderlyingType, visitedTypes);
		}

		if (type == typeof(bool) ||
		    type == typeof(byte) ||
		    type == typeof(sbyte) ||
		    type == typeof(short) ||
		    type == typeof(ushort) ||
		    type == typeof(int) ||
		    type == typeof(uint) ||
		    type == typeof(long) ||
		    type == typeof(ulong) ||
		    type == typeof(float) ||
		    type == typeof(double) ||
		    type == typeof(string) ||
		    type == typeof(Guid)) {
			return true;
		}

		return type.IsValueType && IsSupportedStructType(type, visitedTypes);
	}

	private static Guid GetNetworkObjectTypeId(Type type) {
		NetworkObjectTypeId? typeId = Attribute.GetCustomAttribute(type, typeof(NetworkObjectTypeId), false) as NetworkObjectTypeId;
		if (typeId is null) {
			throw new InvalidOperationException($"Type '{type.FullName}' is missing '{typeof(NetworkObjectTypeId).FullName}'.");
		}

		return typeId.Id;
	}

	private static byte ReadByte(ReadOnlySpan<byte> data, ref int offset) {
		if (data.Length - offset < 1) {
			throw new InvalidOperationException("Upgrade field payload is truncated.");
		}

		byte value = data[offset];
		offset += 1;
		return value;
	}

	private static short ReadInt16(ReadOnlySpan<byte> data, ref int offset) {
		if (data.Length - offset < 2) {
			throw new InvalidOperationException("Upgrade field payload is truncated.");
		}

		short value = BinaryPrimitives.ReadInt16LittleEndian(data[offset..]);
		offset += 2;
		return value;
	}

	private static ushort ReadUInt16(ReadOnlySpan<byte> data, ref int offset) {
		if (data.Length - offset < 2) {
			throw new InvalidOperationException("Upgrade field payload is truncated.");
		}

		ushort value = BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);
		offset += 2;
		return value;
	}

	private static int ReadInt32(ReadOnlySpan<byte> data, ref int offset) {
		if (data.Length - offset < 4) {
			throw new InvalidOperationException("Upgrade field payload is truncated.");
		}

		int value = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
		offset += 4;
		return value;
	}

	private static uint ReadUInt32(ReadOnlySpan<byte> data, ref int offset) {
		if (data.Length - offset < 4) {
			throw new InvalidOperationException("Upgrade field payload is truncated.");
		}

		uint value = BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
		offset += 4;
		return value;
	}

	private static long ReadInt64(ReadOnlySpan<byte> data, ref int offset) {
		if (data.Length - offset < 8) {
			throw new InvalidOperationException("Upgrade field payload is truncated.");
		}

		long value = BinaryPrimitives.ReadInt64LittleEndian(data[offset..]);
		offset += 8;
		return value;
	}

	private static ulong ReadUInt64(ReadOnlySpan<byte> data, ref int offset) {
		if (data.Length - offset < 8) {
			throw new InvalidOperationException("Upgrade field payload is truncated.");
		}

		ulong value = BinaryPrimitives.ReadUInt64LittleEndian(data[offset..]);
		offset += 8;
		return value;
	}

	private static float ReadSingle(ReadOnlySpan<byte> data, ref int offset) {
		if (data.Length - offset < 4) {
			throw new InvalidOperationException("Upgrade field payload is truncated.");
		}

		float value = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]);
		offset += 4;
		return value;
	}

	private static double ReadDouble(ReadOnlySpan<byte> data, ref int offset) {
		if (data.Length - offset < 8) {
			throw new InvalidOperationException("Upgrade field payload is truncated.");
		}

		double value = BinaryPrimitives.ReadDoubleLittleEndian(data[offset..]);
		offset += 8;
		return value;
	}

	private static Guid ReadGuid(ReadOnlySpan<byte> data, ref int offset) {
		if (data.Length - offset < 16) {
			throw new InvalidOperationException("Upgrade field payload is truncated.");
		}

		Guid value = new(data.Slice(offset, 16));
		offset += 16;
		return value;
	}

	private static void WriteByte(BufferWriter writer, byte value) {
		Span<byte> span = writer.GetSpan(1);
		span[0] = value;
		writer.Advance(1);
	}

	private static void WriteUInt16(BufferWriter writer, ushort value) {
		Span<byte> span = writer.GetSpan(2);
		BinaryPrimitives.WriteUInt16LittleEndian(span, value);
		writer.Advance(2);
	}

	private static void WriteInt16(BufferWriter writer, short value) {
		Span<byte> span = writer.GetSpan(2);
		BinaryPrimitives.WriteInt16LittleEndian(span, value);
		writer.Advance(2);
	}

	private static void WriteUInt32(BufferWriter writer, uint value) {
		Span<byte> span = writer.GetSpan(4);
		BinaryPrimitives.WriteUInt32LittleEndian(span, value);
		writer.Advance(4);
	}

	private static void WriteInt32(BufferWriter writer, int value) {
		Span<byte> span = writer.GetSpan(4);
		BinaryPrimitives.WriteInt32LittleEndian(span, value);
		writer.Advance(4);
	}

	private static void WriteUInt64(BufferWriter writer, ulong value) {
		Span<byte> span = writer.GetSpan(8);
		BinaryPrimitives.WriteUInt64LittleEndian(span, value);
		writer.Advance(8);
	}

	private static void WriteInt64(BufferWriter writer, long value) {
		Span<byte> span = writer.GetSpan(8);
		BinaryPrimitives.WriteInt64LittleEndian(span, value);
		writer.Advance(8);
	}

	private static void WriteSingle(BufferWriter writer, float value) {
		Span<byte> span = writer.GetSpan(4);
		BinaryPrimitives.WriteSingleLittleEndian(span, value);
		writer.Advance(4);
	}

	private static void WriteDouble(BufferWriter writer, double value) {
		Span<byte> span = writer.GetSpan(8);
		BinaryPrimitives.WriteDoubleLittleEndian(span, value);
		writer.Advance(8);
	}

	private static void WriteGuid(BufferWriter writer, Guid value) {
		Span<byte> span = writer.GetSpan(16);
		value.TryWriteBytes(span);
		writer.Advance(16);
	}

	private static void WriteUtf8(BufferWriter writer, string value) {
		int byteCount = System.Text.Encoding.UTF8.GetByteCount(value);
		Span<byte> span = writer.GetSpan(byteCount);
		int written = System.Text.Encoding.UTF8.GetBytes(value, span);
		writer.Advance(written);
	}

	private static void WriteBytes(BufferWriter writer, ReadOnlySpan<byte> value) {
		Span<byte> span = writer.GetSpan(value.Length);
		value.CopyTo(span);
		writer.Advance(value.Length);
	}
}
