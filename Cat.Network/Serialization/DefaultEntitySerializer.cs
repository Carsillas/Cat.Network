using Cat.Network.Entities;
using Cat.Network.Generator;
using Cat.Network.Properties;
using System;
using System.Buffers.Binary;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Cat.Network.Serialization;
public class DefaultEntitySerializer : IEntitySerializer {

	private delegate int SpanTypeWriteOperation(ISerializationContext context, Span<byte> buffer, NetworkEntity entity);
	private delegate int SpanTypeReadOperation(ISerializationContext context, ReadOnlySpan<byte> buffer, NetworkEntity entity, out Type type);
	private delegate int SpanPropertyWriteOperation(ISerializationContext context, Span<byte> buffer, NetworkProperty property);
	private delegate int SpanPropertySelectionOperation(ISerializationContext context, ReadOnlySpan<byte> buffer, NetworkProperty[] properties, out NetworkProperty networkProperty);

	private SpanTypeWriteOperation TypeIdentifierWriter { get; }
	private SpanTypeReadOperation TypeIdentifierReader { get; }
	private SpanPropertyWriteOperation MemberIdentifierWriter { get; }
	private SpanPropertySelectionOperation MemberIdentifierReader { get; }
	private MemberSelectionMode MemberSelectionMode { get; }
	private MemberSerializationMode MemberSerializationMode { get; }

	public DefaultEntitySerializer(SerializationOptions options) {

		TypeIdentifierWriter = options.TypeIdentifierMode switch {
			TypeIdentifierMode.None => NoOperation,
			TypeIdentifierMode.FullName => WriteTypeFullName,
			_ => throw new ArgumentOutOfRangeException(nameof(TypeIdentifierMode)),
		};

		TypeIdentifierReader = options.TypeIdentifierMode switch {
			TypeIdentifierMode.None => NoOperation,
			TypeIdentifierMode.FullName => ReadTypeFullName,
			_ => throw new ArgumentOutOfRangeException(nameof(TypeIdentifierMode)),
		};

		MemberIdentifierWriter = options.MemberIdentifierMode switch {
			MemberIdentifierMode.None => NoOperation,
			MemberIdentifierMode.Index => WritePropertyIndex,
			MemberIdentifierMode.Name => WritePropertyName,
			MemberIdentifierMode.HashedName => throw new NotImplementedException(),
			_ => throw new ArgumentOutOfRangeException(nameof(MemberIdentifierMode)),
		};

		MemberIdentifierReader = options.MemberIdentifierMode switch {
			MemberIdentifierMode.None => NoOperation,
			MemberIdentifierMode.Index => SelectPropertyByIndex,
			MemberIdentifierMode.Name => SelectPropertyByName,
			MemberIdentifierMode.HashedName => throw new NotImplementedException(),
			_ => throw new ArgumentOutOfRangeException(nameof(MemberIdentifierMode)),
		};

		MemberSelectionMode = options.MemberSelectionMode;
		MemberSerializationMode = options.MemberSerializationMode;
	}

	public void ReadEntityContent(ISerializationContext context, ReadOnlySpan<byte> buffer, ref NetworkEntity entity) {
		int position = 0;

		position += TypeIdentifierReader(context, buffer, entity, out Type type);

		if (entity == null) {
			entity = (NetworkEntity)Activator.CreateInstance(type);
		}
		INetworkEntityInitializer entityInitializer = entity;
		NetworkProperty[] properties = entityInitializer.NetworkProperties;

		while (position < buffer.Length) {
			position += MemberIdentifierReader(context, buffer.Slice(position), properties, out NetworkProperty property);
			position += ReadPropertyValue(context, buffer.Slice(position), property);
		}
	}

	public int WriteEntityContent(ISerializationContext context, Span<byte> buffer, NetworkEntity entity) {
		int position = 0;

		position += TypeIdentifierWriter(context, buffer.Slice(position), entity);

		INetworkEntityInitializer entityInitializer = entity;
		NetworkProperty[] properties = entityInitializer.NetworkProperties;

		foreach (NetworkProperty property in properties) {

			if (MemberSelectionMode == MemberSelectionMode.Dirty && !property.Dirty) {
				continue;
			}

			position += MemberIdentifierWriter(context, buffer.Slice(position), property);
			position += WritePropertyValue(context, buffer.Slice(position), property);
		}

		return position;
	}

	private static int NoOperation(ISerializationContext _1, Span<byte> _2, NetworkEntity _3) => 0;
	private static int NoOperation(ISerializationContext _1, ReadOnlySpan<byte> _2, NetworkEntity _3, out Type type) {
		type = null;
		return 0;
	}

	private static int NoOperation(ISerializationContext _1, Span<byte> _2, NetworkProperty _3) => 0;
	private static int NoOperation(ISerializationContext _1, ReadOnlySpan<byte> _2, out NetworkProperty networkProperty) {
		networkProperty = null;
		return 0;
	}
	private int NoOperation(ISerializationContext context, ReadOnlySpan<byte> buffer, NetworkProperty[] properties, out NetworkProperty networkProperty) {
		networkProperty = null;
		return 0;
	}

	private static int WriteTypeFullName(ISerializationContext context, Span<byte> buffer, NetworkEntity entity) {
		Span<byte> stringBuffer = buffer.Slice(4);
		int stringByteLength = Encoding.Unicode.GetBytes(entity.GetType().FullName, stringBuffer);
		BinaryPrimitives.WriteInt32LittleEndian(buffer, stringByteLength);
		return stringByteLength + 4;
	}

	private static int ReadTypeFullName(ISerializationContext context, ReadOnlySpan<byte> buffer, NetworkEntity entity, out Type type) {

		int typeNameLength = BinaryPrimitives.ReadInt32LittleEndian(buffer);
		string typeName = Encoding.Unicode.GetString(buffer.Slice(4, typeNameLength));
		Type unverifiedType = Type.GetType(typeName);

		if (unverifiedType == null) {
			throw new Exception("Received Create Entity request with an unresolved type!");
		} else if (unverifiedType.IsSubclassOf(typeof(NetworkEntity))) {
			type = unverifiedType;
		} else {
			throw new Exception("Received Create Entity request with an invalid type!");
		}

		return typeNameLength + 4;
	}



	private static int WritePropertyIndex(ISerializationContext context, Span<byte> buffer, NetworkProperty property) {
		BinaryPrimitives.WriteInt32LittleEndian(buffer, property.Index);
		return 4;
	}

	private int SelectPropertyByIndex(ISerializationContext context, ReadOnlySpan<byte> buffer, NetworkProperty[] properties, out NetworkProperty networkProperty) {
		int propertyIndex = BinaryPrimitives.ReadInt32LittleEndian(buffer);

		networkProperty = properties[propertyIndex];
		return 4;
	}

	private static int WritePropertyName(ISerializationContext context, Span<byte> buffer, NetworkProperty property) {
		Span<byte> stringBuffer = buffer.Slice(4);
		int stringByteLength = Encoding.Unicode.GetBytes(property.Name, stringBuffer);
		BinaryPrimitives.WriteInt32LittleEndian(buffer, stringByteLength);
		return stringByteLength + 4;
	}

	private int SelectPropertyByName(ISerializationContext context, ReadOnlySpan<byte> buffer, NetworkProperty[] properties, out NetworkProperty networkProperty) {

		int propertyNameLength = BinaryPrimitives.ReadInt32LittleEndian(buffer);
		string propertyName = Encoding.Unicode.GetString(buffer.Slice(4, propertyNameLength));

		networkProperty = null;

		foreach (NetworkProperty property in properties) {
			if (property.Name == propertyName) {
				networkProperty = property;
				break;
			}
		}

		return propertyNameLength + 4;
	}

	private int WritePropertyValue(ISerializationContext context, Span<byte> buffer, NetworkProperty property) {
		int length = property.Write(context, MemberSerializationMode, buffer.Slice(4));
		BinaryPrimitives.WriteInt32LittleEndian(buffer, length);
		return length + 4;
	}

	private int ReadPropertyValue(ISerializationContext context, ReadOnlySpan<byte> buffer, NetworkProperty property) {
		int length = BinaryPrimitives.ReadInt32LittleEndian(buffer);
		property.Read(context, MemberSerializationMode, buffer.Slice(4, length));
		return length + 4;
	}




}
