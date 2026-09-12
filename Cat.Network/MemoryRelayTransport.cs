namespace Cat.Network;

public sealed class MemoryRelayTransport : IRelayTransport {
	
	public MemoryRelayTransport? Remote { get; set; }

	public event MessageHandler? MessageReceived;
	public event Action<IRelayTransport>? Disconnected;

	private Queue<byte[]> ReceivedMessages { get; set; } = new();

	public void Send(ReadOnlySpan<byte> message) {
		if (Remote is null) {
			throw new InvalidOperationException("Cannot send a relay message without a remote transport.");
		}

		Remote.ReceivedMessages.Enqueue(message.ToArray());
	}

	public void PumpMessages() {
		while (ReceivedMessages.TryDequeue(out byte[] message)) {
			MessageReceived?.Invoke(this, message);
		}
	}

	internal void PrependReceivedMessages(IEnumerable<byte[]> messages) {
		Queue<byte[]> queuedMessages = new(messages);
		foreach (byte[] message in ReceivedMessages) {
			queuedMessages.Enqueue(message);
		}

		ReceivedMessages = queuedMessages;
	}

	public void Disconnect() {
		Disconnected?.Invoke(this);
	}
}
