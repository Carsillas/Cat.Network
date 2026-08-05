using System.Buffers.Binary;

namespace Cat.Network;

internal static class ReadOnlySpanExtensions {
	private const int GuidSize = 16;

	public static bool TryConsumePacketChannel(this ref ReadOnlySpan<byte> message, out NetworkMessageChannel channel) {
		channel = default;

		if (message.Length < sizeof(NetworkMessageChannel)) {
			return false;
		}

		channel = (NetworkMessageChannel)message[0];
		message = message[sizeof(NetworkMessageChannel)..];

		return true;
	}

	public static bool TryConsumeEntityMessageKind(this ref ReadOnlySpan<byte> message, out EntityMessageKind kind) {
		kind = default;

		if (message.Length < sizeof(EntityMessageKind)) {
			return false;
		}

		kind = (EntityMessageKind)message[0];
		message = message[sizeof(EntityMessageKind)..];

		return true;
	}

	public static bool TryConsumeProfileMessageKind(this ref ReadOnlySpan<byte> message, out ProfileMessageKind kind) {
		kind = default;

		if (message.Length < sizeof(ProfileMessageKind)) {
			return false;
		}

		kind = (ProfileMessageKind)message[0];
		message = message[sizeof(ProfileMessageKind)..];

		return true;
	}

	public static bool TryConsumeGuid(this ref ReadOnlySpan<byte> message, out Guid guid) {
		guid = Guid.Empty;

		if (message.Length < GuidSize) {
			return false;
		}

		guid = new Guid(message[..GuidSize]);
		message = message[GuidSize..];

		return true;
	}

	public static bool TryConsumeUInt64(this ref ReadOnlySpan<byte> message, out ulong value) {
		value = 0;

		if (message.Length < sizeof(ulong)) {
			return false;
		}

		value = BinaryPrimitives.ReadUInt64LittleEndian(message);
		message = message[sizeof(ulong)..];

		return true;
	}

	public static bool TryConsumeLengthPrefixedData(this ref ReadOnlySpan<byte> message, out ReadOnlySpan<byte> data) {
		data = default;

		if (message.Length < sizeof(int)) {
			return false;
		}

		int byteCount = BinaryPrimitives.ReadInt32LittleEndian(message);
		message = message[sizeof(int)..];

		if (byteCount < 0 || message.Length < byteCount) {
			return false;
		}

		data = message[..byteCount];
		message = message[byteCount..];

		return true;
	}
}
