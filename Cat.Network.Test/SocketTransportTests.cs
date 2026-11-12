using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace Cat.Network.Test;

public sealed class SocketTransportTests {
	[TestCase(0u)]
	[TestCase((uint)int.MaxValue + 1)]
	[TestCase(uint.MaxValue)]
	public void Constructor_RejectsInvalidConfiguredPacketLimit(uint maxPacketSize) {
		using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

		ArgumentOutOfRangeException? exception = Assert.Throws<ArgumentOutOfRangeException>(
			() => new SocketTransport(socket, maxPacketSize));

		Assert.That(exception!.ParamName, Is.EqualTo("maxPacketSize"));
	}

	[TestCase(1u)]
	[TestCase(2u)]
	[TestCase(3u)]
	[TestCase(4u)]
	[TestCase(8u)]
	public void Send_EnforcesConfiguredPacketLimit(uint maxPacketSize) {
		using SocketPair sockets = SocketPair.Create();
		using SocketTransport sender = new(sockets.Client, maxPacketSize);
		using SocketTransport receiver = new(sockets.Server, maxPacketSize);
		byte[] message = Enumerable.Range(1, (int)maxPacketSize).Select(value => (byte)value).ToArray();
		List<byte[]> receivedMessages = [];
		receiver.MessageReceived += (_, received) => receivedMessages.Add(received.ToArray());

		ArgumentOutOfRangeException? exception = Assert.Throws<ArgumentOutOfRangeException>(
			() => sender.Send(new byte[message.Length + 1]));
		Assert.That(exception!.ParamName, Is.EqualTo("message"));

		sender.Send([]);
		sender.Send(message);
		WaitForBytes(sockets.Server, 2 * sizeof(int) + message.Length);
		receiver.PumpMessages();

		Assert.That(receivedMessages, Is.EqualTo(new[] { Array.Empty<byte>(), message }));
	}

	[TestCase(1u, false)]
	[TestCase(1u, true)]
	[TestCase(2u, false)]
	[TestCase(2u, true)]
	[TestCase(3u, false)]
	[TestCase(3u, true)]
	[TestCase(4u, true)]
	[TestCase(8u, true)]
	public void PumpMessages_RejectsPacketAboveConfiguredLimit(uint maxPacketSize, bool includeBody) {
		using SocketPair sockets = SocketPair.Create();
		using SocketTransport receiver = new(sockets.Server, maxPacketSize);
		int packetSize = (int)maxPacketSize + 1;
		byte[] frame = new byte[sizeof(int) + (includeBody ? packetSize : 0)];
		BinaryPrimitives.WriteInt32LittleEndian(frame, packetSize);
		int receivedCount = 0;
		int disconnectedCount = 0;
		receiver.MessageReceived += (_, _) => receivedCount++;
		receiver.Disconnected += _ => disconnectedCount++;

		Assert.That(sockets.Client.Send(frame), Is.EqualTo(frame.Length));
		WaitForBytes(sockets.Server, frame.Length);
		receiver.PumpMessages();

		Assert.Multiple(() => {
			Assert.That(disconnectedCount, Is.EqualTo(1));
			Assert.That(receivedCount, Is.Zero);
		});
	}

	[Test]
	public void Send_FramesMessageForPeer() {
		using SocketPair sockets = SocketPair.Create();
		using SocketTransport sender = new(sockets.Client);
		using SocketTransport receiver = new(sockets.Server);
		List<byte[]> receivedMessages = [];

		receiver.MessageReceived += (_, message) => receivedMessages.Add(message.ToArray());

		sender.Send([1, 2, 3]);
		receiver.PumpMessages();

		Assert.That(receivedMessages, Is.EqualTo(new[] { new byte[] { 1, 2, 3 } }));
	}

	[Test]
	public void PumpMessages_DeliversMultipleAvailableMessages() {
		using SocketPair sockets = SocketPair.Create();
		using SocketTransport sender = new(sockets.Client);
		using SocketTransport receiver = new(sockets.Server);
		List<byte[]> receivedMessages = [];

		receiver.MessageReceived += (_, message) => receivedMessages.Add(message.ToArray());

		sender.Send([1, 2, 3]);
		sender.Send([4, 5]);
		receiver.PumpMessages();

		Assert.That(receivedMessages, Is.EqualTo(new[] {
			new byte[] { 1, 2, 3 },
			new byte[] { 4, 5 }
		}));
	}

	[Test]
	public void PumpMessages_WaitsForCompleteMessage() {
		using SocketPair sockets = SocketPair.Create();
		using SocketTransport receiver = new(sockets.Server);
		byte[] framedMessage = [5, 0, 0, 0, 1, 2, 3];
		int receivedCount = 0;

		receiver.MessageReceived += (_, _) => receivedCount++;

		sockets.Client.Send(framedMessage);
		receiver.PumpMessages();

		Assert.That(receivedCount, Is.EqualTo(0));

		sockets.Client.Send(new byte[] { 4, 5 });
		receiver.PumpMessages();

		Assert.That(receivedCount, Is.EqualTo(1));
	}

	[Test]
	public void PumpMessages_RaisesDisconnectedForInvalidPacketSize() {
		using SocketPair sockets = SocketPair.Create();
		using SocketTransport receiver = new(sockets.Server, maxPacketSize: 3);
		int disconnectedCount = 0;

		receiver.Disconnected += disconnectedTransport => {
			Assert.That(disconnectedTransport, Is.SameAs(receiver));
			disconnectedCount++;
		};

		sockets.Client.Send([5, 0, 0, 0]);
		receiver.PumpMessages();

		Assert.That(disconnectedCount, Is.EqualTo(1));
	}

	[Test]
	public void Dispose_RaisesDisconnectedOnce() {
		using SocketPair sockets = SocketPair.Create();
		SocketTransport transport = new(sockets.Server);
		int disconnectedCount = 0;

		transport.Disconnected += _ => disconnectedCount++;

		transport.Dispose();
		transport.Dispose();

		Assert.That(disconnectedCount, Is.EqualTo(1));
	}

	private static void WaitForBytes(Socket socket, int count) {
		Assert.That(SpinWait.SpinUntil(() => socket.Available >= count, TimeSpan.FromSeconds(5)),
			Is.True, "The peer did not receive the expected frame bytes.");
	}

	private sealed class SocketPair : IDisposable {
		private SocketPair(Socket client, Socket server) {
			Client = client;
			Server = server;
		}

		public Socket Client { get; }
		public Socket Server { get; }

		public static SocketPair Create() {
			TcpListener listener = new(IPAddress.Loopback, 0);
			listener.Start();

			try {
				IPEndPoint endpoint = (IPEndPoint)listener.LocalEndpoint;
				Socket client = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) {
					NoDelay = true
				};
				client.Connect(endpoint);

				Socket server = listener.AcceptSocket();
				server.NoDelay = true;
				return new SocketPair(client, server);
			} finally {
				listener.Stop();
			}
		}

		public void Dispose() {
			Client.Dispose();
			Server.Dispose();
		}
	}
}
