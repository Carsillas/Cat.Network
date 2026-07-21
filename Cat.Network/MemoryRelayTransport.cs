namespace Cat.Network;

public sealed class MemoryRelayTransport : IRelayTransport {
	private readonly Queue<byte[]> incomingMessages = [];

	public MemoryRelayTransport? Remote { get; set; }

	public event MessageHandler? MessageReceived;

	public void Send(ReadOnlySpan<byte> message) {
		if (Remote is null) {
			throw new InvalidOperationException("Cannot send a relay message without a remote transport.");
		}

		Remote.incomingMessages.Enqueue(message.ToArray());
	}

	public void PumpMessages() {
		while (incomingMessages.TryDequeue(out byte[]? message)) {
			MessageReceived?.Invoke(this, message);
		}
	}
}
