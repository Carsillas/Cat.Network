using System.Buffers.Binary;

namespace Cat.Network;

public abstract partial class RelayPeer {
	private void ProcessEntityMessage(IRelayTransport sender, ReadOnlySpan<byte> message) {
		if (!TryExtractEntityMessageKind(ref message, out EntityMessageKind kind)) {
			return;
		}

		if (!TryExtractGuid(ref message, out Guid entityId)) {
			return;
		}

		switch (kind) {
			case EntityMessageKind.AssignOwner:
				OnAssignOwnerMessage(sender, entityId);
				break;
			case EntityMessageKind.RequestOwnershipTransfer:
				if (TryExtractGuid(ref message, out Guid ownerProfileId)) {
					OnOwnershipTransferRequestMessage(sender, entityId, ownerProfileId);
				}

				break;
			case EntityMessageKind.Create:
				if (!TryExtractGuid(ref message, out Guid typeId)) {
					return;
				}

				if (TryExtractLengthPrefixedData(ref message, out ReadOnlySpan<byte> createData)) {
					OnCreateEntityMessage(sender, entityId, typeId, createData);
				}

				break;
			case EntityMessageKind.Update:
				if (TryExtractLengthPrefixedData(ref message, out ReadOnlySpan<byte> updateData)) {
					OnUpdateEntityMessage(sender, entityId, updateData);
				}

				break;
			case EntityMessageKind.Delete:
				OnDeleteEntityMessage(sender, entityId);
				break;
			case EntityMessageKind.Rpc:
				if (TryExtractLengthPrefixedData(ref message, out ReadOnlySpan<byte> rpcData)) {
					OnRpcMessage(sender, entityId, rpcData);
				}

				break;
			case EntityMessageKind.Broadcast:
				if (TryExtractLengthPrefixedData(ref message, out ReadOnlySpan<byte> broadcastData)) {
					OnBroadcastMessage(sender, entityId, broadcastData);
				}

				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
		}
	}

	protected virtual void OnAssignOwnerMessage(IRelayTransport sender, Guid entityId) {
	}

	protected virtual void OnOwnershipTransferRequestMessage(IRelayTransport sender, Guid entityId, Guid ownerProfileId) {
	}

	protected virtual void OnCreateEntityMessage(IRelayTransport sender, Guid entityId, Guid typeId, ReadOnlySpan<byte> data) {
	}

	protected virtual void OnUpdateEntityMessage(IRelayTransport sender, Guid entityId, ReadOnlySpan<byte> data) {
	}

	protected virtual void OnDeleteEntityMessage(IRelayTransport sender, Guid entityId) {
	}

	protected virtual void OnRpcMessage(IRelayTransport sender, Guid entityId, ReadOnlySpan<byte> data) {
	}

	protected virtual void OnBroadcastMessage(IRelayTransport sender, Guid entityId, ReadOnlySpan<byte> data) {
	}

	protected bool TryCreateEntity(Guid entityId, Guid typeId, ReadOnlySpan<byte> data, out NetworkEntity entity) {
		entity = null!;

		if (!TypeCatalogue.TryFindType(typeId, out Type? type) ||
		    !typeof(NetworkEntity).IsAssignableFrom(type) ||
		    !TypeCatalogue.TryFindSerializer(type, out INetworkObjectSerializer? serializer) ||
		    Activator.CreateInstance(type) is not NetworkEntity target) {
			return false;
		}

		target.Id = entityId;
		serializer.Deserialize(target, data, new SerializationContext(TypeCatalogue));
		entity = target;
		return true;
	}

	protected bool TryDeserializeEntityUpdate(NetworkEntity entity, ReadOnlySpan<byte> data) {
		if (!TypeCatalogue.TryFindSerializer(entity.GetType(), out INetworkObjectSerializer? serializer)) {
			return false;
		}

		serializer.Deserialize(entity, data, new SerializationContext(TypeCatalogue));
		return true;
	}

	protected bool TryWriteCreateEntityMessage(BufferWriter writer, NetworkEntity entity) {
		if (!TypeCatalogue.TryFindTypeId(entity.GetType(), out Guid typeId) ||
		    !TypeCatalogue.TryFindSerializer(entity.GetType(), out INetworkObjectSerializer? serializer)) {
			return false;
		}

		WriteEntityMessageHeader(writer, EntityMessageKind.Create, entity.Id);
		WriteGuid(writer, typeId);
		Range lengthRange = writer.Reserve(sizeof(int));
		int dataStart = writer.WrittenCount;
		serializer.Serialize(writer, entity, new SerializationContext(TypeCatalogue), new SerializationOptions(MemberSelectionMode.All, MemberIdentificationMode.Index));
		BinaryPrimitives.WriteInt32LittleEndian(writer.GetSpan(lengthRange), writer.WrittenCount - dataStart);
		return true;
	}

	protected bool TryWriteUpdateEntityMessage(BufferWriter writer, NetworkEntity entity) {
		if (!TypeCatalogue.TryFindSerializer(entity.GetType(), out INetworkObjectSerializer? serializer)) {
			return false;
		}

		WriteEntityMessageHeader(writer, EntityMessageKind.Update, entity.Id);
		Range lengthRange = writer.Reserve(sizeof(int));
		int dataStart = writer.WrittenCount;
		serializer.Serialize(writer, entity, new SerializationContext(TypeCatalogue), new SerializationOptions(MemberSelectionMode.Dirty, MemberIdentificationMode.Index));
		BinaryPrimitives.WriteInt32LittleEndian(writer.GetSpan(lengthRange), writer.WrittenCount - dataStart);
		return true;
	}

	protected static void WriteDeleteEntityMessage(BufferWriter writer, Guid entityId) {
		WriteEntityMessageHeader(writer, EntityMessageKind.Delete, entityId);
	}

	protected static void WriteAssignOwnerMessage(BufferWriter writer, Guid entityId) {
		WriteEntityMessageHeader(writer, EntityMessageKind.AssignOwner, entityId);
	}

	protected static void WriteOwnershipTransferRequestMessage(BufferWriter writer, Guid entityId, Guid ownerProfileId) {
		WriteEntityMessageHeader(writer, EntityMessageKind.RequestOwnershipTransfer, entityId);
		WriteGuid(writer, ownerProfileId);
	}

	private static void WriteEntityMessageHeader(BufferWriter writer, EntityMessageKind kind, Guid entityId) {
		WriteByte(writer, (byte)NetworkMessageChannel.EntityMessage);
		WriteByte(writer, (byte)kind);
		WriteGuid(writer, entityId);
	}

	private static bool TryExtractEntityMessageKind(ref ReadOnlySpan<byte> message, out EntityMessageKind kind) {
		kind = default;

		if (message.Length < sizeof(EntityMessageKind)) {
			return false;
		}

		kind = (EntityMessageKind)message[0];
		message = message[sizeof(EntityMessageKind)..];

		return true;
	}
}
