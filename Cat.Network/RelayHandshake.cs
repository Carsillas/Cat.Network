namespace Cat.Network;

internal static class RelayHandshake {
	public static ReadOnlySpan<byte> Ping => [0];
	public static ReadOnlySpan<byte> Pong => [1];

	public static bool IsPing(ReadOnlySpan<byte> message) {
		return message.SequenceEqual(Ping);
	}

	public static bool IsPong(ReadOnlySpan<byte> message) {
		return message.SequenceEqual(Pong);
	}
}
