using System.Diagnostics.CodeAnalysis;

namespace Cat.Network;

public sealed class MemoryRelayDaemon : IDaemon {
	private readonly List<PendingConnection> pendingConnections = [];
	private readonly Queue<AcceptedConnection> acceptedConnections = [];
	private readonly Dictionary<IRelayTransport, PendingConnection> pendingConnectionsByTransport = [];
	private Func<NetworkProfile> ProfileFactory { get; }

	public MemoryRelayDaemon(Func<NetworkProfile> profileFactory) {
		ProfileFactory = profileFactory ?? throw new ArgumentNullException(nameof(profileFactory));
	}

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
				if (connection.BufferedMessages.Count > 0) {
					// Deliver application input only after the accepting peer can subscribe.
					connection.Transport.PrependReceivedMessages(connection.BufferedMessages);
				}

				acceptedConnections.Enqueue(new AcceptedConnection(connection.Transport, ProfileFactory()));
			}
		}
	}

	public bool TryAcceptConnection([NotNullWhen(true)] out IRelayTransport? transport, [NotNullWhen(true)] out NetworkProfile? profile) {
		if (acceptedConnections.TryDequeue(out AcceptedConnection? connection)) {
			transport = connection.Transport;
			profile = connection.Profile;
			return true;
		}

		transport = null;
		profile = null;
		return false;
	}

	private void OnPendingConnectionMessageReceived(IRelayTransport sender, ReadOnlySpan<byte> message) {
		if (!pendingConnectionsByTransport.TryGetValue(sender, out PendingConnection? connection)) {
			return;
		}

		if (RelayHandshake.IsPong(message)) {
			connection.Accepted = true;
		} else if (!RelayHandshake.IsPing(message)) {
			connection.BufferedMessages.Enqueue(message.ToArray());
		}
	}

	private sealed class PendingConnection(MemoryRelayTransport transport) {
		public MemoryRelayTransport Transport { get; } = transport;
		public Queue<byte[]> BufferedMessages { get; } = new();
		public bool PingSent { get; set; }
		public bool Accepted { get; set; }
	}

	private sealed class AcceptedConnection(IRelayTransport transport, NetworkProfile profile) {
		public IRelayTransport Transport { get; } = transport;
		public NetworkProfile Profile { get; } = profile;
	}
}
