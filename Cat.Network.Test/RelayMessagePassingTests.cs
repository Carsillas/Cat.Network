using System.Text;

namespace Cat.Network.Test;

public sealed class RelayMessagePassingTests {
	[Test]
	public void TickRelaysClientMessageToOtherClients() {
		var daemon = new MemoryRelayDaemon();
		var server = new RelayServer(daemon);
		var clientA = new RelayClient();
		var clientB = new RelayClient();
		Connect(daemon, server, clientA);
		Connect(daemon, server, clientB);
		clientA.Send("hello"u8);
		clientA.Tick();
		server.Tick();
		clientB.Tick();

		Assert.Multiple(() => {
			Assert.That(clientB.TryReadMessage(out ReadOnlyMemory<byte> received), Is.True);
			Assert.That(Encoding.UTF8.GetString(received.Span), Is.EqualTo("hello"));
			Assert.That(clientB.TryReadMessage(out ReadOnlyMemory<byte> _), Is.False);
		});
	}

	[Test]
	public void TickDoesNotEchoMessageBackToSender() {
		var daemon = new MemoryRelayDaemon();
		var server = new RelayServer(daemon);
		var clientA = new RelayClient();
		var clientB = new RelayClient();
		Connect(daemon, server, clientA);
		Connect(daemon, server, clientB);

		clientA.Send("hello"u8);
		clientA.Tick();
		server.Tick();
		clientA.Tick();

		Assert.That(clientA.TryReadMessage(out ReadOnlyMemory<byte> _), Is.False);
	}

	[Test]
	public void RemoveTransportStopsMessagesFromBeingRelayedToThatTransport() {
		var daemon = new MemoryRelayDaemon();
		var server = new RelayServer(daemon);
		var clientA = new RelayClient();
		var clientB = new RelayClient();
		Connect(daemon, server, clientA);
		MemoryRelayTransport clientBTransport = Connect(daemon, server, clientB);
		server.RemoveTransport(clientBTransport.Remote!);

		clientA.Send("hello"u8);
		clientA.Tick();
		server.Tick();
		clientB.Tick();

		Assert.That(clientB.TryReadMessage(out ReadOnlyMemory<byte> _), Is.False);
	}

	[Test]
	public void ServerDoesNotRelayMessagesBeforeHandshakeCompletes() {
		var daemon = new MemoryRelayDaemon();
		var server = new RelayServer(daemon);
		var clientA = new RelayClient();
		var clientB = new RelayClient();
		MemoryRelayTransport clientATransport = daemon.Connect();
		MemoryRelayTransport clientBTransport = daemon.Connect();
		clientA.Connect(clientATransport);
		clientB.Connect(clientBTransport);

		clientA.Send("hello"u8);
		clientA.Tick();
		server.Tick();
		clientB.Tick();

		Assert.That(clientB.TryReadMessage(out ReadOnlyMemory<byte> _), Is.False);
	}

	private static MemoryRelayTransport Connect(MemoryRelayDaemon daemon, RelayServer server, RelayClient client) {
		MemoryRelayTransport clientTransport = daemon.Connect();
		client.Connect(clientTransport);

		server.Tick();
		client.Tick();
		server.Tick();

		return clientTransport;
	}
}
