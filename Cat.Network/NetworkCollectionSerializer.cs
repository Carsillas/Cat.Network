using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;

namespace Cat.Network;

internal static class NetworkCollectionSerializer {
	private static ConcurrentDictionary<Type, ItemCodec> Codecs { get; } = new();
	// Struct fields need boundaries independent of the enclosing collection item.
	private static ItemCodec StructStringFieldCodec { get; } = new(
		isNetworkObject: false,
		serializeFull: static (writer, value, _, _) => WriteStructStringField(writer, (string?)value),
		serializeUpdate: static (writer, value, _, _) => WriteStructStringField(writer, (string?)value),
		deserializeFull: static (ReadOnlySpan<byte> data, ref int offset, SerializationContext _) => ReadStructStringField(data, ref offset),
		deserializeUpdate: static (_, _, _) => throw new InvalidOperationException("String struct fields do not support update operations."));

	public static ItemCodec GetCodec<T>() {
		return ItemCodecCache<T>.Value;
	}

	public static bool IsNetworkObjectItemType<T>() {
		return ItemCodecCache<T>.Value.IsNetworkObject;
	}

	public static bool HasDirtyState(NetworkObject item) {
		return ((INetworkObject)item).PropertyStates.Any(static state => state != NetworkPropertyState.Unchanged);
	}

	private static ItemCodec GetOrCreateCodec(Type declaredType) {
		return Codecs.GetOrAdd(declaredType, BuildCodec);
	}

	private static ItemCodec BuildCodec(Type declaredType) {
		Type? nullableUnderlyingType = Nullable.GetUnderlyingType(declaredType);
		if (nullableUnderlyingType is not null) {
			ItemCodec underlyingCodec = GetOrCreateCodec(nullableUnderlyingType);
			return new ItemCodec(
				isNetworkObject: false,
				serializeFull: (writer, value, context, options) => {
					if (value is null) {
						writer.WriteByte(0);
						return;
					}

					writer.WriteByte(1);
					underlyingCodec.SerializeFullObject(writer, value, context, options);
				},
				serializeUpdate: (writer, value, context, options) => {
					if (value is null) {
						writer.WriteByte(0);
						return;
					}

					writer.WriteByte(1);
					underlyingCodec.SerializeUpdateObject(writer, value, context, options);
				},
				deserializeFull: (ReadOnlySpan<byte> data, ref int offset, SerializationContext context) => {
					if (!TryReadByte(data, ref offset, out byte hasValue)) {
						throw new InvalidOperationException("Collection item nullable payload is truncated.");
					}

					return hasValue switch {
						0 => null,
						1 => underlyingCodec.DeserializeFullObject(data, ref offset, context),
						_ => throw new InvalidOperationException("Collection item nullable flag is invalid.")
					};
				},
				deserializeUpdate: static (_, _, _) => throw new InvalidOperationException("Nullable collection items do not support update operations."));
		}

		if (declaredType == typeof(bool)) {
			return new ItemCodec(
				isNetworkObject: false,
				serializeFull: static (writer, value, _, _) => writer.WriteByte((bool)value! ? (byte)1 : (byte)0),
				serializeUpdate: static (writer, value, _, _) => writer.WriteByte((bool)value! ? (byte)1 : (byte)0),
				deserializeFull: static (ReadOnlySpan<byte> data, ref int offset, SerializationContext _) => ReadByte(data, ref offset) != 0,
				deserializeUpdate: static (_, _, _) => throw new InvalidOperationException("Boolean collection items do not support update operations."));
		}

		if (declaredType == typeof(byte)) {
			return new ItemCodec(
				isNetworkObject: false,
				serializeFull: static (writer, value, _, _) => writer.WriteByte((byte)value!),
				serializeUpdate: static (writer, value, _, _) => writer.WriteByte((byte)value!),
				deserializeFull: static (ReadOnlySpan<byte> data, ref int offset, SerializationContext _) => ReadByte(data, ref offset),
				deserializeUpdate: static (_, _, _) => throw new InvalidOperationException("Byte collection items do not support update operations."));
		}

		if (declaredType == typeof(sbyte)) {
			return new ItemCodec(
				isNetworkObject: false,
				serializeFull: static (writer, value, _, _) => writer.WriteByte(unchecked((byte)(sbyte)value!)),
				serializeUpdate: static (writer, value, _, _) => writer.WriteByte(unchecked((byte)(sbyte)value!)),
				deserializeFull: static (ReadOnlySpan<byte> data, ref int offset, SerializationContext _) => unchecked((sbyte)ReadByte(data, ref offset)),
				deserializeUpdate: static (_, _, _) => throw new InvalidOperationException("SByte collection items do not support update operations."));
		}

		if (declaredType == typeof(short)) {
			return new ItemCodec(
				isNetworkObject: false,
				serializeFull: static (writer, value, _, _) => writer.WriteInt16((short)value!),
				serializeUpdate: static (writer, value, _, _) => writer.WriteInt16((short)value!),
				deserializeFull: static (ReadOnlySpan<byte> data, ref int offset, SerializationContext _) => ReadInt16(data, ref offset),
				deserializeUpdate: static (_, _, _) => throw new InvalidOperationException("Int16 collection items do not support update operations."));
		}

		if (declaredType == typeof(ushort)) {
			return new ItemCodec(
				isNetworkObject: false,
				serializeFull: static (writer, value, _, _) => writer.WriteUInt16((ushort)value!),
				serializeUpdate: static (writer, value, _, _) => writer.WriteUInt16((ushort)value!),
				deserializeFull: static (ReadOnlySpan<byte> data, ref int offset, SerializationContext _) => ReadUInt16(data, ref offset),
				deserializeUpdate: static (_, _, _) => throw new InvalidOperationException("UInt16 collection items do not support update operations."));
		}

		if (declaredType == typeof(int)) {
			return new ItemCodec(
				isNetworkObject: false,
				serializeFull: static (writer, value, _, _) => writer.WriteInt32((int)value!),
				serializeUpdate: static (writer, value, _, _) => writer.WriteInt32((int)value!),
				deserializeFull: static (ReadOnlySpan<byte> data, ref int offset, SerializationContext _) => ReadInt32(data, ref offset),
				deserializeUpdate: static (_, _, _) => throw new InvalidOperationException("Int32 collection items do not support update operations."));
		}

		if (declaredType == typeof(uint)) {
			return new ItemCodec(
				isNetworkObject: false,
				serializeFull: static (writer, value, _, _) => writer.WriteUInt32((uint)value!),
				serializeUpdate: static (writer, value, _, _) => writer.WriteUInt32((uint)value!),
				deserializeFull: static (ReadOnlySpan<byte> data, ref int offset, SerializationContext _) => ReadUInt32(data, ref offset),
				deserializeUpdate: static (_, _, _) => throw new InvalidOperationException("UInt32 collection items do not support update operations."));
		}

		if (declaredType == typeof(long)) {
			return new ItemCodec(
				isNetworkObject: false,
				serializeFull: static (writer, value, _, _) => writer.WriteInt64((long)value!),
				serializeUpdate: static (writer, value, _, _) => writer.WriteInt64((long)value!),
				deserializeFull: static (ReadOnlySpan<byte> data, ref int offset, SerializationContext _) => ReadInt64(data, ref offset),
				deserializeUpdate: static (_, _, _) => throw new InvalidOperationException("Int64 collection items do not support update operations."));
		}

		if (declaredType == typeof(ulong)) {
			return new ItemCodec(
				isNetworkObject: false,
				serializeFull: static (writer, value, _, _) => writer.WriteUInt64((ulong)value!),
				serializeUpdate: static (writer, value, _, _) => writer.WriteUInt64((ulong)value!),
				deserializeFull: static (ReadOnlySpan<byte> data, ref int offset, SerializationContext _) => ReadUInt64(data, ref offset),
				deserializeUpdate: static (_, _, _) => throw new InvalidOperationException("UInt64 collection items do not support update operations."));
		}

		if (declaredType == typeof(float)) {
			return new ItemCodec(
				isNetworkObject: false,
				serializeFull: static (writer, value, _, _) => writer.WriteSingle((float)value!),
				serializeUpdate: static (writer, value, _, _) => writer.WriteSingle((float)value!),
				deserializeFull: static (ReadOnlySpan<byte> data, ref int offset, SerializationContext _) => ReadSingle(data, ref offset),
				deserializeUpdate: static (_, _, _) => throw new InvalidOperationException("Single collection items do not support update operations."));
		}

		if (declaredType == typeof(double)) {
			return new ItemCodec(
				isNetworkObject: false,
				serializeFull: static (writer, value, _, _) => writer.WriteDouble((double)value!),
				serializeUpdate: static (writer, value, _, _) => writer.WriteDouble((double)value!),
				deserializeFull: static (ReadOnlySpan<byte> data, ref int offset, SerializationContext _) => ReadDouble(data, ref offset),
				deserializeUpdate: static (_, _, _) => throw new InvalidOperationException("Double collection items do not support update operations."));
		}

		if (declaredType == typeof(string)) {
			return new ItemCodec(
				isNetworkObject: false,
				serializeFull: static (writer, value, _, _) => writer.WriteUtf8((string)value!),
				serializeUpdate: static (writer, value, _, _) => writer.WriteUtf8((string)value!),
				deserializeFull: static (ReadOnlySpan<byte> data, ref int offset, SerializationContext _) => {
					string value = System.Text.Encoding.UTF8.GetString(data[offset..]);
					offset = data.Length;
					return value;
				},
				deserializeUpdate: static (_, _, _) => throw new InvalidOperationException("String collection items do not support update operations."));
		}

		if (declaredType == typeof(Guid)) {
			return new ItemCodec(
				isNetworkObject: false,
				serializeFull: static (writer, value, _, _) => writer.WriteGuid((Guid)value!),
				serializeUpdate: static (writer, value, _, _) => writer.WriteGuid((Guid)value!),
				deserializeFull: static (ReadOnlySpan<byte> data, ref int offset, SerializationContext _) => {
					if (data.Length - offset < 16) {
						throw new InvalidOperationException("Collection item Guid payload is truncated.");
					}

					Guid value = new(data.Slice(offset, 16));
					offset += 16;
					return value;
				},
				deserializeUpdate: static (_, _, _) => throw new InvalidOperationException("Guid collection items do not support update operations."));
		}

		if (typeof(NetworkObject).IsAssignableFrom(declaredType)) {
			return new ItemCodec(
				isNetworkObject: true,
				serializeFull: static (writer, value, context, options) => {
					if (value is null) {
						writer.WriteByte(0);
						return;
					}

					writer.WriteByte(1);
					NetworkObject networkObject = (NetworkObject)value;
					if (!context.TypeCatalogue.TryFindSerializer(networkObject.GetType(), out INetworkObjectSerializer? serializer)) {
						throw new InvalidOperationException($"Serializer for type '{networkObject.GetType().FullName}' is not registered.");
					}

					writer.WriteGuid(GetNetworkObjectTypeId(networkObject.GetType()));
					serializer.Serialize(
						writer,
						networkObject,
						context,
						new SerializationOptions(MemberSelectionMode.All, options.MemberIdentificationMode));
				},
				serializeUpdate: static (writer, value, context, options) => {
					if (value is not NetworkObject networkObject) {
						throw new InvalidOperationException("Collection item type does not support update operations.");
					}

					if (!context.TypeCatalogue.TryFindSerializer(networkObject.GetType(), out INetworkObjectSerializer? serializer)) {
						throw new InvalidOperationException($"Serializer for type '{networkObject.GetType().FullName}' is not registered.");
					}

					serializer.Serialize(
						writer,
						networkObject,
						context,
						new SerializationOptions(MemberSelectionMode.Dirty, options.MemberIdentificationMode));
				},
				deserializeFull: (ReadOnlySpan<byte> data, ref int offset, SerializationContext context) => {
					if (!TryReadByte(data, ref offset, out byte hasValue)) {
						throw new InvalidOperationException("Collection item object payload is truncated.");
					}

					if (hasValue == 0) {
						return null;
					}

					if (data.Length - offset < 16) {
						throw new InvalidOperationException("Collection item type id payload is truncated.");
					}

					Guid typeId = new(data.Slice(offset, 16));
					offset += 16;
					if (!context.TypeCatalogue.TryFindType(typeId, out Type? runtimeType)) {
						throw new InvalidOperationException($"Type id '{typeId}' is not registered.");
					}

					if (!declaredType.IsAssignableFrom(runtimeType)) {
						throw new InvalidOperationException($"Type '{runtimeType.FullName}' is not assignable to '{declaredType.FullName}'.");
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
				},
				deserializeUpdate: static (item, data, context) => {
					if (item is not NetworkObject networkObject) {
						throw new InvalidOperationException("Collection item type does not support update operations.");
					}

					if (!context.TypeCatalogue.TryFindSerializer(networkObject.GetType(), out INetworkObjectSerializer? serializer)) {
						throw new InvalidOperationException($"Serializer for type '{networkObject.GetType().FullName}' is not registered.");
					}

					serializer.Deserialize(networkObject, data, context);
				});
		}

		if (declaredType.IsValueType) {
			FieldCodec[] fieldCodecs = declaredType.GetFields(BindingFlags.Instance | BindingFlags.Public)
				.Where(static field => !field.IsStatic)
				.OrderBy(static field => field.Name, StringComparer.Ordinal)
				.Select(field => new FieldCodec(field, field.FieldType == typeof(string) ? StructStringFieldCodec : GetOrCreateCodec(field.FieldType)))
				.ToArray();

			return new ItemCodec(
				isNetworkObject: false,
				serializeFull: (writer, value, context, options) => {
					foreach (FieldCodec fieldCodec in fieldCodecs) {
						fieldCodec.Codec.SerializeFullObject(writer, fieldCodec.Field.GetValue(value!), context, options);
					}
				},
				serializeUpdate: (writer, value, context, options) => {
					foreach (FieldCodec fieldCodec in fieldCodecs) {
						fieldCodec.Codec.SerializeUpdateObject(writer, fieldCodec.Field.GetValue(value!), context, options);
					}
				},
				deserializeFull: (ReadOnlySpan<byte> data, ref int offset, SerializationContext context) => {
					object boxed = Activator.CreateInstance(declaredType)!;
					foreach (FieldCodec fieldCodec in fieldCodecs) {
						object? fieldValue = fieldCodec.Codec.DeserializeFullObject(data, ref offset, context);
						fieldCodec.Field.SetValue(boxed, fieldValue);
					}

					return boxed;
				},
				deserializeUpdate: static (_, _, _) => throw new InvalidOperationException("Struct collection items do not support update operations."));
		}

		throw new InvalidOperationException($"Collection item type '{declaredType.FullName}' is not supported.");
	}

	private static void WriteStructStringField(BufferWriter writer, string? value) {
		if (value is null) {
			writer.WriteByte(0);
			return;
		}

		writer.WriteByte(1);
		writer.WriteLengthPrefixedUtf8(value);
	}

	private static string? ReadStructStringField(ReadOnlySpan<byte> data, ref int offset) {
		byte hasValue = ReadByte(data, ref offset);
		if (hasValue == 0) {
			return null;
		}
		if (hasValue != 1) {
			throw new InvalidOperationException("Collection struct string nullable flag is invalid.");
		}

		uint byteCount = ReadUInt32(data, ref offset);
		if (data.Length - offset < byteCount) {
			throw new InvalidOperationException("Collection struct string payload is truncated.");
		}

		string value = System.Text.Encoding.UTF8.GetString(data.Slice(offset, (int)byteCount));
		offset += (int)byteCount;
		return value;
	}

	private static Guid GetNetworkObjectTypeId(Type type) {
		NetworkObjectTypeId? typeId = Attribute.GetCustomAttribute(type, typeof(NetworkObjectTypeId), false) as NetworkObjectTypeId;
		if (typeId is null) {
			throw new InvalidOperationException($"Type '{type.FullName}' is missing '{typeof(NetworkObjectTypeId).FullName}'.");
		}

		return typeId.Id;
	}

	private static bool TryReadByte(ReadOnlySpan<byte> data, ref int offset, out byte value) {
		if (data.Length - offset < 1) {
			value = 0;
			return false;
		}

		value = data[offset];
		offset += 1;
		return true;
	}

	private static byte ReadByte(ReadOnlySpan<byte> data, ref int offset) {
		if (!TryReadByte(data, ref offset, out byte value)) {
			throw new InvalidOperationException("Collection item payload is truncated.");
		}

		return value;
	}

	private static short ReadInt16(ReadOnlySpan<byte> data, ref int offset) {
		if (data.Length - offset < 2) {
			throw new InvalidOperationException("Collection item payload is truncated.");
		}

		short value = BinaryPrimitives.ReadInt16LittleEndian(data[offset..]);
		offset += 2;
		return value;
	}

	private static ushort ReadUInt16(ReadOnlySpan<byte> data, ref int offset) {
		if (data.Length - offset < 2) {
			throw new InvalidOperationException("Collection item payload is truncated.");
		}

		ushort value = BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);
		offset += 2;
		return value;
	}

	private static int ReadInt32(ReadOnlySpan<byte> data, ref int offset) {
		if (data.Length - offset < 4) {
			throw new InvalidOperationException("Collection item payload is truncated.");
		}

		int value = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
		offset += 4;
		return value;
	}

	private static uint ReadUInt32(ReadOnlySpan<byte> data, ref int offset) {
		if (data.Length - offset < 4) {
			throw new InvalidOperationException("Collection item payload is truncated.");
		}

		uint value = BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
		offset += 4;
		return value;
	}

	private static long ReadInt64(ReadOnlySpan<byte> data, ref int offset) {
		if (data.Length - offset < 8) {
			throw new InvalidOperationException("Collection item payload is truncated.");
		}

		long value = BinaryPrimitives.ReadInt64LittleEndian(data[offset..]);
		offset += 8;
		return value;
	}

	private static ulong ReadUInt64(ReadOnlySpan<byte> data, ref int offset) {
		if (data.Length - offset < 8) {
			throw new InvalidOperationException("Collection item payload is truncated.");
		}

		ulong value = BinaryPrimitives.ReadUInt64LittleEndian(data[offset..]);
		offset += 8;
		return value;
	}

	private static float ReadSingle(ReadOnlySpan<byte> data, ref int offset) {
		if (data.Length - offset < 4) {
			throw new InvalidOperationException("Collection item payload is truncated.");
		}

		float value = BinaryPrimitives.ReadSingleLittleEndian(data[offset..]);
		offset += 4;
		return value;
	}

	private static double ReadDouble(ReadOnlySpan<byte> data, ref int offset) {
		if (data.Length - offset < 8) {
			throw new InvalidOperationException("Collection item payload is truncated.");
		}

		double value = BinaryPrimitives.ReadDoubleLittleEndian(data[offset..]);
		offset += 8;
		return value;
	}

	private readonly record struct FieldCodec(FieldInfo Field, ItemCodec Codec);

	internal sealed class ItemCodec {
		public ItemCodec(bool isNetworkObject, SerializeObjectDelegate serializeFull, SerializeObjectDelegate serializeUpdate, DeserializeObjectDelegate deserializeFull, DeserializeUpdatedObjectDelegate deserializeUpdate) {
			IsNetworkObject = isNetworkObject;
			SerializeFullObject = serializeFull;
			SerializeUpdateObject = serializeUpdate;
			DeserializeFullObject = deserializeFull;
			DeserializeUpdateObject = deserializeUpdate;
		}

		public bool IsNetworkObject { get; }

		public SerializeObjectDelegate SerializeFullObject { get; }

		public SerializeObjectDelegate SerializeUpdateObject { get; }

		public DeserializeObjectDelegate DeserializeFullObject { get; }

		public DeserializeUpdatedObjectDelegate DeserializeUpdateObject { get; }

		public void SerializeFull<T>(BufferWriter writer, T item, SerializationContext context, SerializationOptions options) {
			SerializeFullObject(writer, item, context, options);
		}

		public void SerializeUpdate<T>(BufferWriter writer, T item, SerializationContext context, SerializationOptions options) {
			SerializeUpdateObject(writer, item, context, options);
		}

		public T DeserializeFull<T>(ReadOnlySpan<byte> data, SerializationContext context) {
			int offset = 0;
			object? value = DeserializeFullObject(data, ref offset, context);
			if (offset != data.Length) {
				throw new InvalidOperationException("Collection item payload was not fully consumed.");
			}

			return (T)value!;
		}

		public void DeserializeUpdate<T>(T item, ReadOnlySpan<byte> data, SerializationContext context) {
			DeserializeUpdateObject(item!, data, context);
		}
	}

	private static class ItemCodecCache<T> {
		public static ItemCodec Value { get; } = GetOrCreateCodec(typeof(T));
	}

	internal delegate void SerializeObjectDelegate(BufferWriter writer, object? value, SerializationContext context, SerializationOptions options);

	internal delegate object? DeserializeObjectDelegate(ReadOnlySpan<byte> data, ref int offset, SerializationContext context);

	internal delegate void DeserializeUpdatedObjectDelegate(object item, ReadOnlySpan<byte> data, SerializationContext context);
}
