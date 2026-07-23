using System.Buffers.Binary;
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
		byte[] packet = CreateApplicationPacket("hello"u8);
		clientA.Send(packet);
		clientA.Tick();
		server.Tick();
		clientB.Tick();

		Assert.Multiple(() => {
			Assert.That(clientB.TryReadMessage(out ReadOnlyMemory<byte> received), Is.True);
			Assert.That(received.ToArray(), Is.EqualTo(packet));
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

		clientA.Send(CreateApplicationPacket("hello"u8));
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

		clientA.Send(CreateApplicationPacket("hello"u8));
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

		clientA.Send(CreateApplicationPacket("hello"u8));
		clientA.Tick();
		server.Tick();
		clientB.Tick();

		Assert.That(clientB.TryReadMessage(out ReadOnlyMemory<byte> _), Is.False);
	}

	[Test]
	public void ServerDoesNotRelayPacketsWithInvalidHeader() {
		var daemon = new MemoryRelayDaemon();
		var server = new RelayServer(daemon);
		var clientA = new RelayClient();
		var clientB = new RelayClient();
		Connect(daemon, server, clientA);
		Connect(daemon, server, clientB);

		byte[] invalidPacket = CreateApplicationPacket("hello"u8);
		BinaryPrimitives.WriteUInt32LittleEndian(invalidPacket, 99);

		clientA.Send(invalidPacket);
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

	private static byte[] CreateApplicationPacket(ReadOnlySpan<byte> payload) {
		byte[] packet = new byte[sizeof(uint) + sizeof(byte) + payload.Length];
		BinaryPrimitives.WriteUInt32LittleEndian(packet, (uint)(sizeof(byte) + payload.Length));
		packet[sizeof(uint)] = (byte)NetworkMessageChannel.Application;
		payload.CopyTo(packet.AsSpan((int)(sizeof(uint) + sizeof(byte))));
		return packet;
	}
}
