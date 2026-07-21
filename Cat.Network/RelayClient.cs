namespace Cat.Network;

public class RelayClient {
	
	private IRelayTransport? transport;
	private bool handshakeCompleted;
	

	public void Send(ReadOnlySpan<byte> message) {
		outgoingMessages.Enqueue(message.ToArray());
	}

	public void Connect(IRelayTransport transport) {
		if (this.transport is not null) {
			this.transport.MessageReceived -= ProcessMessage;
		}

		this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
		this.transport.MessageReceived += ProcessMessage;
		handshakeCompleted = false;
	}

	public void Disconnect() {
		if (transport is not null) {
			transport.MessageReceived -= ProcessMessage;
		}

		transport = null;
		handshakeCompleted = false;
	}

	public void Tick() {
		while (outgoingMessages.TryDequeue(out byte[]? message)) {
			if (transport is null) {
				throw new InvalidOperationException("Cannot send relay messages before connecting to a server.");
			}

			transport.Send(message);
		}

		if (transport is not null) {
			transport.PumpMessages();
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
			transport?.Send(RelayHandshake.Pong);
			handshakeCompleted = true;
			return;
		}

		incomingMessages.Enqueue(message.ToArray());
	}
}
