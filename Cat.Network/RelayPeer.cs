using System.Buffers.Binary;

namespace Cat.Network;

public abstract class RelayPeer {
	private const int GuidSize = 16;
	private TypeCatalogue TypeCatalogue { get; }
	private IEntityStorage EntityStorage { get; }
	
	private protected RelayPeer(TypeCatalogue typeCatalogue, IEntityStorage entityStorage) {
		ArgumentNullException.ThrowIfNull(typeCatalogue);
		ArgumentNullException.ThrowIfNull(entityStorage);
		TypeCatalogue = typeCatalogue.Clone();
		EntityStorage = entityStorage;
	}
	
	protected void ProcessMessage(IRelayTransport sender, ReadOnlySpan<byte> message) {
		if (!TryExtractPacketChannel(ref message, out NetworkMessageChannel channel)) {
			return;
		}

		switch (channel) {
			case NetworkMessageChannel.Application:
				break;
			case NetworkMessageChannel.EntityMessage:
				ProcessEntityMessage(sender, message);
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}
	}

	protected void ProcessEntityMessage(IRelayTransport sender, ReadOnlySpan<byte> message) {
		if (!TryExtractEntityMessageKind(ref message, out EntityMessageKind kind)) {
			return;
		}
		
		if (!TryExtractEntityId(ref message, out Guid entityId)) {
			return;
		}
		
		switch (kind) {
			case EntityMessageKind.Create: {
				if (!TryExtractTypeId(ref message, out Guid typeId)) {
					return;
				}

				if (!TypeCatalogue.TryFindType(typeId, out Type? type)) {
					return;
				}
				
				if (!TypeCatalogue.TryFindSerializer(type, out INetworkObjectSerializer? serializer)) {
					return;
				}

				NetworkEntity target = (NetworkEntity)Activator.CreateInstance(type)!;

				if (!TryExtractObjectData(ref message, out ReadOnlySpan<byte> data)) {
					return;
				}
				
				target.Id = entityId;
				serializer.Deserialize(target, data, new SerializationContext(TypeCatalogue));
				EntityStorage.RegisterEntity(target);
				
				break;
			}
			case EntityMessageKind.Update: {
				if (!EntityStorage.TryGetEntity(entityId, out NetworkEntity? entity)) {
					return;
				}
				if (!TypeCatalogue.TryFindSerializer(entity.GetType(), out INetworkObjectSerializer? serializer)) {
					return;
				}

				if (!TryExtractObjectData(ref message, out ReadOnlySpan<byte> data)) {
					return;
				}
				
				serializer.Deserialize(entity, data, new SerializationContext(TypeCatalogue));
				break;
			}
				
			case EntityMessageKind.Delete:
				break;
			case EntityMessageKind.Rpc:
				break;
			case EntityMessageKind.Broadcast:
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}
		
	}
	
	private static bool TryExtractPacketChannel(ref ReadOnlySpan<byte> message, out NetworkMessageChannel channel) {
		channel = default;

		if (message.Length < sizeof(NetworkMessageChannel)) {
			return false;
		}

		channel = (NetworkMessageChannel)message[0];
		message = message[sizeof(NetworkMessageChannel)..];
		
		return true;
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

	private static bool TryExtractEntityId(ref ReadOnlySpan<byte> message, out Guid guid) {
		guid = Guid.Empty;
		
		if (message.Length < GuidSize) {
			return false;
		}
		
		guid = new Guid(message[..GuidSize]);
		message = message[GuidSize..];
		
		return true;
	}

	private static bool TryExtractTypeId(ref ReadOnlySpan<byte> message, out Guid typeId) {
		return TryExtractEntityId(ref message, out typeId);
	}

	private static bool TryExtractObjectData(ref ReadOnlySpan<byte> message, out ReadOnlySpan<byte> data) {
		data = default;
		
		if (message.Length < sizeof(int)) {
			return false;
		}

		int objectDataByteCount = BinaryPrimitives.ReadInt32LittleEndian(message);
		message = message[sizeof(int)..];

		if (objectDataByteCount < 0 || message.Length < objectDataByteCount) {
			return false;
		}
		
		data = message[..objectDataByteCount];
		message = message[objectDataByteCount..];

		return true;
	}
	
}
