using System.Buffers.Binary;

namespace Cat.Network;

public abstract partial class RelayPeer {
	private const int GuidSize = 16;

	protected TypeCatalogue TypeCatalogue { get; }
	protected BufferWriter MessageWriter { get; } = new();

	private protected RelayPeer(TypeCatalogue typeCatalogue) {
		ArgumentNullException.ThrowIfNull(typeCatalogue);
		TypeCatalogue = typeCatalogue.Clone();
	}

	internal abstract bool Owns(NetworkEntity entity);

	protected void ProcessMessage(IRelayTransport sender, ReadOnlySpan<byte> message) {
		if (RelayHandshake.IsPing(message)) {
			sender.Send(RelayHandshake.Pong);
			return;
		}

		if (RelayHandshake.IsPong(message)) {
			return;
		}

		if (!TryExtractPacketChannel(ref message, out NetworkMessageChannel channel)) {
			return;
		}

		switch (channel) {
			case NetworkMessageChannel.Application:
				OnApplicationMessage(sender, message);
				break;
			case NetworkMessageChannel.EntityMessage:
				ProcessEntityMessage(sender, message);
				break;
			case NetworkMessageChannel.ProfileMessage:
				ProcessProfileMessage(sender, message);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
		}
	}

	protected virtual void OnApplicationMessage(IRelayTransport sender, ReadOnlySpan<byte> message) {
	}

	protected static bool HasDirtyState(NetworkObject target) {
		return ((INetworkObject)target).PropertyStates.Any(static state => state != NetworkPropertyState.Unchanged);
	}

	protected static void ClearDirtyState(NetworkObject target) {
		Array.Fill(((INetworkObject)target).PropertyStates, NetworkPropertyState.Unchanged);
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

	private static bool TryExtractGuid(ref ReadOnlySpan<byte> message, out Guid guid) {
		guid = Guid.Empty;

		if (message.Length < GuidSize) {
			return false;
		}

		guid = new Guid(message[..GuidSize]);
		message = message[GuidSize..];

		return true;
	}

	private static bool TryExtractLengthPrefixedData(ref ReadOnlySpan<byte> message, out ReadOnlySpan<byte> data) {
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

	private static void WriteByte(BufferWriter writer, byte value) {
		Span<byte> span = writer.GetSpan(1);
		span[0] = value;
		writer.Advance(1);
	}

	private static void WriteGuid(BufferWriter writer, Guid value) {
		Span<byte> span = writer.GetSpan(GuidSize);
		value.TryWriteBytes(span);
		writer.Advance(GuidSize);
	}
}
