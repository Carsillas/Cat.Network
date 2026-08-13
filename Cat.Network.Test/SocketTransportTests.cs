using System.Net;
using System.Net.Sockets;

namespace Cat.Network.Test;

public sealed class SocketTransportTests {
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
