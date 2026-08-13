namespace Cat.Network;

public sealed class MemoryRelayTransport : IRelayTransport {
	
	public MemoryRelayTransport? Remote { get; set; }

	public event MessageHandler? MessageReceived;
	public event Action<IRelayTransport>? Disconnected;

	private Queue<byte[]> ReceivedMessages { get; } = new();

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

	public void Disconnect() {
		Disconnected?.Invoke(this);
	}
}
