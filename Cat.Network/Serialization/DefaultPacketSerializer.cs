using Cat.Network.Entities;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Serialization;
public class DefaultPacketSerializer : IPacketSerializer {

	private SerializationOptions UpdateOptions { get; } = new SerializationOptions {
		TypeIdentifierMode = TypeIdentifierMode.None,
		MemberIdentifierMode = MemberIdentifierMode.Index,
		MemberSelectionMode = MemberSelectionMode.Dirty,
		MemberSerializationMode = MemberSerializationMode.Partial
	};
	private SerializationOptions CreateOptions { get; } = new SerializationOptions {
		TypeIdentifierMode = TypeIdentifierMode.AssemblyQualifiedName,
		MemberIdentifierMode = MemberIdentifierMode.Index,
		MemberSelectionMode = MemberSelectionMode.All,
		MemberSerializationMode = MemberSerializationMode.Complete
	};

	private IEntitySerializer UpdateEntitySerializer { get; }

	private IEntitySerializer CreateEntitySerializer { get; }

	public DefaultPacketSerializer() {
		UpdateEntitySerializer = new DefaultEntitySerializer(UpdateOptions);
		CreateEntitySerializer = new DefaultEntitySerializer(CreateOptions);
	}

	public NetworkEntity ReadCreateEntity(Guid networkID, ReadOnlySpan<byte> content) {
		NetworkEntity entity = null;

		CreateEntitySerializer.ReadEntityContent(null, content, ref entity);
		entity.NetworkID = networkID;

		return entity;
	}

	public void ReadUpdateEntity(NetworkEntity targetEntity, ReadOnlySpan<byte> content) {
		UpdateEntitySerializer.ReadEntityContent(null, content, ref targetEntity);
	}


	public int WriteCreateEntity(NetworkEntity targetEntity, Span<byte> content) {
		
		Span<byte> contentLength = content.Slice(0, 4);
		Span<byte> contentData = content.Slice(4);

		int length = CreateEntitySerializer.WriteEntityContent(null, contentData, targetEntity);
		BinaryPrimitives.WriteInt32LittleEndian(contentLength, length);

		return length + 4;
	}

	public int WriteUpdateEntity(NetworkEntity targetEntity, Span<byte> content) {

		Span<byte> contentLength = content.Slice(0, 4);
		Span<byte> contentData = content.Slice(4);

		int length = UpdateEntitySerializer.WriteEntityContent(null, contentData, targetEntity);
		BinaryPrimitives.WriteInt32LittleEndian(contentLength, length);

		return length + 4;
	}

}
