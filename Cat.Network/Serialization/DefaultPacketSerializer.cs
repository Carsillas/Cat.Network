using Cat.Network.Entities;
using System;
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
		TypeIdentifierMode = TypeIdentifierMode.FullName,
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

	public NetworkEntity CreateEntity(Guid networkID, ReadOnlySpan<byte> content) {
		NetworkEntity entity = null;

		CreateEntitySerializer.ReadEntityContent(null, content, ref entity);

		return entity;
	}

	public void UpdateEntity(NetworkEntity targetEntity, ReadOnlySpan<byte> content) {
		UpdateEntitySerializer.ReadEntityContent(null, content, ref targetEntity);
	}
}
