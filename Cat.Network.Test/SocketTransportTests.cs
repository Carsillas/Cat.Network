using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Cat.Network.Test;

public sealed class SocketTransportTests {
	private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

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

	[TestCase(0)]
	[TestCase(1)]
	[TestCase(2)]
	[TestCase(3)]
	[TestCase(4)]
	[TestCase(6)]
	[TestCase(8)]
	public void PumpMessages_DisconnectsForIdleOrTruncatedPeerClosure(int sentByteCount) {
		using SocketPair sockets = SocketPair.Create();
		using SocketTransport receiver = new(sockets.Server);
		byte[] framedMessage = [5, 0, 0, 0, 1, 2, 3, 4, 5];
		int receivedCount = 0;
		int disconnectedCount = 0;
		receiver.MessageReceived += (_, _) => receivedCount++;
		receiver.Disconnected += _ => disconnectedCount++;

		if (sentByteCount > 0) {
			Assert.That(sockets.Client.Send(framedMessage.AsSpan(0, sentByteCount)), Is.EqualTo(sentByteCount));
			WaitForAvailableBytes(sockets.Server, sentByteCount);
		}
		receiver.PumpMessages();
		Assert.That(disconnectedCount, Is.Zero, "An incomplete frame from an open peer must remain pending.");

		sockets.Client.Shutdown(SocketShutdown.Send);
		PumpUntil(receiver, () => disconnectedCount > 0);
		receiver.Dispose();

		Assert.That(receivedCount, Is.Zero, "A truncated frame must never be delivered.");
		Assert.That(disconnectedCount, Is.EqualTo(1));
	}

	[Test]
	public void PumpMessages_DeliversCompleteFramesBeforePeerClosure() {
		using SocketPair sockets = SocketPair.Create();
		using SocketTransport receiver = new(sockets.Server);
		byte[] framedMessages = [3, 0, 0, 0, 1, 2, 3, 0, 0, 0, 0, 2, 0];
		List<byte[]> receivedMessages = [];
		int disconnectedCount = 0;
		receiver.MessageReceived += (_, message) => receivedMessages.Add(message.ToArray());
		receiver.Disconnected += _ => disconnectedCount++;

		Assert.That(sockets.Client.Send(framedMessages), Is.EqualTo(framedMessages.Length));
		sockets.Client.Shutdown(SocketShutdown.Send);
		PumpUntil(receiver, () => disconnectedCount > 0);

		Assert.That(receivedMessages, Is.EqualTo(new[] { new byte[] { 1, 2, 3 }, Array.Empty<byte>() }));
		Assert.That(disconnectedCount, Is.EqualTo(1));
	}

	[Test]
	public void PumpMessages_DrainsFragmentedHeadersAndBodiesAcrossPumps() {
		using SocketPair sockets = SocketPair.Create();
		using SocketTransport receiver = new(sockets.Server);
		List<byte[]> receivedMessages = [];
		receiver.MessageReceived += (_, message) => receivedMessages.Add(message.ToArray());
		byte[][] fragments = [[5], [0, 0], [0, 1, 2], [3, 4], [5, 0, 0], [0], [0, 3], [0, 0, 0], [6], [7, 8]];
		int[] expectedCounts = [0, 0, 0, 0, 1, 1, 2, 2, 2, 3];

		for (int i = 0; i < fragments.Length; i++) {
			byte[] fragment = fragments[i];
			Assert.That(sockets.Client.Send(fragment), Is.EqualTo(fragment.Length));
			WaitForAvailableBytes(sockets.Server, fragment.Length);
			receiver.PumpMessages();

			Assert.That(sockets.Server.Available, Is.Zero, "Each pump must drain the available fragment.");
			Assert.That(receivedMessages, Has.Count.EqualTo(expectedCounts[i]));
		}

		Assert.That(receivedMessages, Is.EqualTo(new[] {
			new byte[] { 1, 2, 3, 4, 5 },
			Array.Empty<byte>(),
			new byte[] { 6, 7, 8 }
		}));
	}

	[Test]
	public void PumpMessages_ReceivesFrameLargerThanSocketBuffers() {
		using SocketPair sockets = SocketPair.Create();
		sockets.Client.SendBufferSize = 4096;
		sockets.Client.Blocking = false;
		sockets.Server.ReceiveBufferSize = 4096;
		using SocketTransport receiver = new(sockets.Server);
		List<byte[]> receivedMessages = [];
		receiver.MessageReceived += (_, message) => receivedMessages.Add(message.ToArray());
		const int messageSize = 512 * 1024;
		byte[] framedMessage = new byte[sizeof(int) + messageSize];
		BinaryPrimitives.WriteInt32LittleEndian(framedMessage, messageSize);
		for (int i = sizeof(int); i < framedMessage.Length; i++) {
			framedMessage[i] = (byte)(i % 251);
		}
		Assert.That(sockets.Server.ReceiveBufferSize, Is.LessThan(messageSize));
		int sentByteCount = 0;
		Stopwatch elapsed = Stopwatch.StartNew();

		while (receivedMessages.Count == 0 && elapsed.Elapsed < TestTimeout) {
			if (sentByteCount < framedMessage.Length) {
				try {
					sentByteCount += sockets.Client.Send(framedMessage.AsSpan(sentByteCount), SocketFlags.None);
				} catch (SocketException exception) when (exception.SocketErrorCode == SocketError.WouldBlock) {
					// Keep draining the receiver while the constrained sender is backpressured.
				}
			}
			receiver.PumpMessages();
			if (receivedMessages.Count == 0) {
				Thread.Sleep(1);
			}
		}

		Assert.That(receivedMessages, Has.Count.EqualTo(1), "The frame must complete within the bounded receive loop.");
		Assert.That(sentByteCount, Is.EqualTo(framedMessage.Length));
		Assert.That(receivedMessages[0], Is.EqualTo(framedMessage[sizeof(int)..]));
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

	private static void WaitForAvailableBytes(Socket socket, int byteCount) {
		Assert.That(SpinWait.SpinUntil(() => socket.Available >= byteCount, TestTimeout), Is.True,
			"The loopback fragment did not arrive before the timeout.");
	}

	private static void PumpUntil(SocketTransport transport, Func<bool> completed) {
		Stopwatch elapsed = Stopwatch.StartNew();
		while (!completed() && elapsed.Elapsed < TestTimeout) {
			transport.PumpMessages();
			if (!completed()) {
				Thread.Sleep(1);
			}
		}
		Assert.That(completed(), Is.True, "The transport did not make progress before the timeout.");
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
					NoDelay = true,
					SendTimeout = 1000,
					ReceiveTimeout = 1000
				};
				client.ConnectAsync(endpoint).WaitAsync(TestTimeout).GetAwaiter().GetResult();

				Socket server = listener.AcceptSocketAsync().WaitAsync(TestTimeout).GetAwaiter().GetResult();
				server.NoDelay = true;
				server.SendTimeout = 1000;
				server.ReceiveTimeout = 1000;
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
