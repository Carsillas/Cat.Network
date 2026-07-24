namespace Cat.Network;

public class RelayClient : RelayPeer {
	
	private IRelayTransport? Transport { get; set; }

	public void Send(ReadOnlySpan<byte> message) {
		
	}

	public void Connect(IRelayTransport transport) {
		if (Transport is not null) {
			Transport.MessageReceived -= ProcessMessage;
		}

		Transport = transport ?? throw new ArgumentNullException(nameof(transport));
		Transport.MessageReceived += ProcessMessage;
	}

	public void Disconnect() {
		if (Transport is not null) {
			Transport.MessageReceived -= ProcessMessage;
		}

		Transport = null;
	}

	public void Tick() {
		if (Transport is not null) {
			Transport.PumpMessages();
		}
		
		while (outgoingMessages.TryDequeue(out byte[]? message)) {
			if (Transport is null) {
				throw new InvalidOperationException("Cannot send relay messages before connecting to a server.");
			}

			Transport.Send(message);
		}

	}

	public bool TryReadMessage(out ReadOnlyMemory<byte> message) {
		if (incomingMessages.TryDequeue(out byte[]? bytes)) {
			message = bytes;
			return true;
		}

		message = default;
		return false;
	}

	private void ProcessMessage(IRelayTransport sender, ReadOnlySpan<byte> message) {
		if (!handshakeCompleted && RelayHandshake.IsPing(message)) {
			Transport?.Send(RelayHandshake.Pong);
			handshakeCompleted = true;
			return;
		}

		incomingMessages.Enqueue(message.ToArray());
	}
}
