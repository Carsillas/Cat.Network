namespace Cat.Network;

public sealed class MemoryRelayDaemon : IDaemon {
	private readonly List<PendingConnection> pendingConnections = [];
	private readonly Queue<IRelayTransport> acceptedConnections = [];
	private readonly Dictionary<IRelayTransport, PendingConnection> pendingConnectionsByTransport = [];

	public MemoryRelayTransport Connect() {
		var clientTransport = new MemoryRelayTransport();
		var serverTransport = new MemoryRelayTransport();
		clientTransport.Remote = serverTransport;
		serverTransport.Remote = clientTransport;
		var connection = new PendingConnection(serverTransport);
		serverTransport.MessageReceived += OnPendingConnectionMessageReceived;
		pendingConnections.Add(connection);
		pendingConnectionsByTransport.Add(serverTransport, connection);

		return clientTransport;
	}

	public void Tick() {
		for (int i = pendingConnections.Count - 1; i >= 0; i--) {
			PendingConnection connection = pendingConnections[i];

			if (!connection.PingSent) {
				connection.Transport.Send(RelayHandshake.Ping);
				connection.PingSent = true;
			}

			connection.Transport.PumpMessages();

			if (connection.Accepted) {
				pendingConnections.RemoveAt(i);
				connection.Transport.MessageReceived -= OnPendingConnectionMessageReceived;
				pendingConnectionsByTransport.Remove(connection.Transport);
				acceptedConnections.Enqueue(connection.Transport);
			}
		}
	}

	public bool TryAcceptConnection(out IRelayTransport transport) {
		return acceptedConnections.TryDequeue(out transport!);
	}

	private void OnPendingConnectionMessageReceived(IRelayTransport sender, ReadOnlySpan<byte> message) {
		if (!pendingConnectionsByTransport.TryGetValue(sender, out PendingConnection? connection)) {
			return;
		}

		if (RelayHandshake.IsPong(message)) {
			connection.Accepted = true;
		}
	}

	private sealed class PendingConnection(IRelayTransport transport) {
		public IRelayTransport Transport { get; } = transport;
		public bool PingSent { get; set; }
		public bool Accepted { get; set; }
	}
}
