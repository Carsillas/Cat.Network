using Cat.Network.Entities;
using Cat.Network.Generator;
using Cat.Network.Properties;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Serialization;
public class DefaultEntitySerializer : IEntitySerializer {

	private delegate int SpanTypeWriteOperation(ISerializationContext context, NetworkEntity entity, Span<byte> buffer, int offset);
	private delegate int SpanPropertyWriteOperation(ISerializationContext context, NetworkProperty property, Span<byte> buffer, int offset);

	private SpanTypeWriteOperation TypeIdentifierWriter { get; }
	private SpanPropertyWriteOperation MemberIdentifierWriter { get; }
	private MemberSelectionMode MemberSelectionMode { get; }
	private MemberSerializationMode MemberSerializationMode { get; }

	public DefaultEntitySerializer(SerializationOptions options) {
		
		TypeIdentifierWriter = options.TypeIdentifierMode switch {
			TypeIdentifierMode.None => NoOperation,
			TypeIdentifierMode.FullName => WriteTypeFullName,
			_ => throw new ArgumentOutOfRangeException(nameof(TypeIdentifierMode)),
		};

		MemberIdentifierWriter  = options.MemberIdentifierMode switch {
			MemberIdentifierMode.None => NoOperation,
			MemberIdentifierMode.Index => WritePropertyIndex,
			MemberIdentifierMode.Name => WritePropertyName,
			MemberIdentifierMode.HashedName => throw new NotImplementedException(),
			_ => throw new ArgumentOutOfRangeException(nameof(MemberIdentifierMode)),
		};

		MemberSelectionMode = options.MemberSelectionMode;
		MemberSerializationMode = options.MemberSerializationMode;
	}

	public void ReadEntityContent(ISerializationContext context, NetworkEntity entity, ReadOnlySpan<byte> buffer) {

	}

	public int WriteEntityContent(ISerializationContext context, NetworkEntity entity, Span<byte> buffer) {
		int position = 0;
		
		position += TypeIdentifierWriter.Invoke(context, entity, buffer, position);

		NetworkProperty[] properties = Unsafe.As<INetworkEntityInitializer>(entity).NetworkProperties;

		foreach(NetworkProperty property in properties) {

			if (MemberSelectionMode == MemberSelectionMode.Dirty && !property.Dirty) {
				continue;
			}

			position += MemberIdentifierWriter(context, property, buffer, position);
			position += WritePropertyValue(context, property, buffer, position);
		}

		return position;
	}

	private static int NoOperation(ISerializationContext _1, NetworkEntity _2, Span<byte> _3, int _4) => 0;
	private static int NoOperation(ISerializationContext _1, NetworkProperty _2, Span<byte> _3, int _4) => 0;


	private static int WriteTypeFullName(ISerializationContext context, NetworkEntity entity, Span<byte> buffer, int offset) {
		Span<byte> stringBuffer = buffer.Slice(4);
		int stringByteLength = Encoding.Unicode.GetBytes(entity.GetType().FullName, stringBuffer);
		BinaryPrimitives.WriteInt32LittleEndian(buffer, stringByteLength);
		return stringByteLength + 4;
	}

	private static int WritePropertyIndex(ISerializationContext context, NetworkProperty property, Span<byte> buffer, int offset) {
		BinaryPrimitives.WriteInt32LittleEndian(buffer, property.Index);
		return 4;
	}

	private static int WritePropertyName(ISerializationContext context, NetworkProperty property, Span<byte> buffer, int offset) {
		return Encoding.Unicode.GetBytes(property.Name, buffer);
	}

	private int WritePropertyValue(ISerializationContext context, NetworkProperty property, Span<byte> buffer, int offset) {
		return property.Write(context, MemberSerializationMode, buffer, offset);
	}

}
