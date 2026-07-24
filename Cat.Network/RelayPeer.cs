using System.Buffers.Binary;
using System.Text;

namespace Cat.Network;

public abstract class RelayPeer {
	
	private const int GuidSize = 16;
	
	private protected RelayPeer() { }
	
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
				if (!TryExtractTypeName(ref message, out string? name)) {
					return;
				}
				
				if (!TryExtractObjectData(ref message, out ReadOnlySpan<byte> data)) {
					return;
				}
				
				// TODO create entity and register
				
				break;
			}
			case EntityMessageKind.Update: {
				if (!TryExtractObjectData(ref message, out ReadOnlySpan<byte> data)) {
					return;
				}
				
				ApplyChanges()
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

	private static bool TryExtractTypeName(ref ReadOnlySpan<byte> message, out string? name) {
		name = null;

		if (message.Length < sizeof(int)) {
			return false;
		}
		
		int stringByteCount = BinaryPrimitives.ReadInt32LittleEndian(message);
		message = message[sizeof(int)..];

		if (stringByteCount < 0 || message.Length < stringByteCount) {
			return false;
		}
		
		name = Encoding.UTF8.GetString(message[..stringByteCount]);
		message = message[stringByteCount..];
		
		return true;
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
	
	private static bool ApplyChanges(NetworkObject target, ReadOnlySpan<byte> data) {
		
		
		
		return false;
	}
}