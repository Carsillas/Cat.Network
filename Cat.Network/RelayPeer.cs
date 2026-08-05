namespace Cat.Network;

public abstract partial class RelayPeer {
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

		if (!message.TryConsumePacketChannel(out NetworkMessageChannel channel)) {
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
}
