using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Cat.Network;

public class SocketTransport : ITransport, IDisposable {

	private enum NetworkReadState {
		Waiting,
		Success
	}

	private ILogger Logger { get; }
	private Socket Socket { get; }
	private byte[] ReceiveBuffer { get; } = new byte[1_000_000];

	IEnumerator<NetworkReadState> ReceiveEnumerator { get; }


	public SocketTransport(ILogger logger, Socket socket) {
		Logger = logger;
		Socket = socket;
		ReceiveEnumerator = ReadAvailablePackets();
	}

	public void SendPacket(byte[] buffer, int count) {
		int totalBytesSent = 0;

		Span<byte> packetLengthBuffer = stackalloc byte[4];
		BinaryPrimitives.WriteInt32LittleEndian(packetLengthBuffer, count);

		// send packet length
		while (totalBytesSent < 4) {
			int bytesSent = Socket.Send(packetLengthBuffer.Slice(totalBytesSent), SocketFlags.None);

			if (bytesSent == 0) {
				throw new SocketException();
			}

			totalBytesSent += bytesSent;
		}

		// send content
		totalBytesSent = 0;
		while (totalBytesSent < count) {

			int bytesSent = Socket.Send(buffer, totalBytesSent, count - totalBytesSent, SocketFlags.None);

			if (bytesSent == 0) {
				throw new SocketException();
			}

			totalBytesSent += bytesSent;
		}
	}

	public void ReadIncomingPackets(PacketProcessor packetProcessor) {
		while(ReceiveEnumerator.MoveNext() && ReceiveEnumerator.Current == NetworkReadState.Success) {
			packetProcessor(ReceiveBuffer);
		}
	}

	private IEnumerator<NetworkReadState> ReadAvailablePackets() {

		while (true) {

			//Not enough for the length prefix, skip reading
			while (Socket.Available < 4) {
				yield return NetworkReadState.Waiting;
			}

			int packetSize;
			try {
				//Read packet size
				int HeaderBytesRead = 0;
				while (HeaderBytesRead < 4) {
					HeaderBytesRead += Socket.Receive(ReceiveBuffer, HeaderBytesRead, 4 - HeaderBytesRead, SocketFlags.None);
				}
				packetSize = BinaryPrimitives.ReadInt32LittleEndian(ReceiveBuffer);

			} catch (Exception e) {
				Logger.LogError(e, "Exception occurred while reading packet!");
				Dispose();
				yield break;
			}

			if (packetSize > ReceiveBuffer.Length) {	
				Logger.LogError("Encountered a packet with extreme size: {Size} bytes.", packetSize);
				Dispose();
				yield break;
			}

			//Not enough for the packet, skip reading
			while (Socket.Available < packetSize) {
				yield return NetworkReadState.Waiting;
			}

			try {
				//Read data
				int contentBytesRead = 0;
				while (contentBytesRead < packetSize) {
					contentBytesRead += Socket.Receive(ReceiveBuffer, contentBytesRead, packetSize - contentBytesRead, SocketFlags.None);
				}
			} catch (Exception e) {
				Console.WriteLine(e);
				Dispose();
				yield break;
			}

			yield return NetworkReadState.Success;
		}

	}


	public void Dispose() {
		Logger.LogInformation("Disconnecting from {Remote}", Socket.RemoteEndPoint);
		Socket.Dispose();
	}
}
