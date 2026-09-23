using System.Buffers.Binary;
using System.Net.Sockets;

namespace Cat.Network;

public sealed class SocketTransport : IRelayTransport, IDisposable {
	private const int LengthPrefixSize = sizeof(int);
	private const uint DefaultMaxPacketSize = 1_048_576;
	private const int WaitingForPacket = -1;

	private Socket Socket { get; }
	private byte[] ReceiveBuffer { get; }
	private IEnumerator<int> ReceiveEnumerator { get; }
	private bool Disposed { get; set; }

	public SocketTransport(Socket socket, uint maxPacketSize = DefaultMaxPacketSize) {
		ArgumentNullException.ThrowIfNull(socket);
		if (maxPacketSize == 0 || maxPacketSize > int.MaxValue) {
			throw new ArgumentOutOfRangeException(nameof(maxPacketSize));
		}

		Socket = socket;
		ReceiveBuffer = new byte[Math.Max(LengthPrefixSize, (int)maxPacketSize)];
		ReceiveEnumerator = ReadAvailablePackets();
	}

	public event MessageHandler? MessageReceived;
	public event Action<IRelayTransport>? Disconnected;

	public void Send(ReadOnlySpan<byte> message) {
		ObjectDisposedException.ThrowIf(Disposed, this);
		if (message.Length > ReceiveBuffer.Length) {
			throw new ArgumentOutOfRangeException(nameof(message));
		}

		Span<byte> lengthPrefix = stackalloc byte[LengthPrefixSize];
		BinaryPrimitives.WriteInt32LittleEndian(lengthPrefix, message.Length);
		SendAll(lengthPrefix);
		SendAll(message);
	}

	public void PumpMessages() {
		ObjectDisposedException.ThrowIf(Disposed, this);

		while (ReceiveEnumerator.MoveNext() && ReceiveEnumerator.Current != WaitingForPacket) {
			MessageReceived?.Invoke(this, ReceiveBuffer.AsSpan(0, ReceiveEnumerator.Current));
		}
	}

	public void Dispose() {
		if (Disposed) {
			return;
		}

		Disposed = true;
		Socket.Close();
		Socket.Dispose();
		Disconnected?.Invoke(this);
	}

	private IEnumerator<int> ReadAvailablePackets() {
		while (true) {
			int receivedByteCount = 0;
			while (receivedByteCount < LengthPrefixSize) {
				int bytesRead = ReceiveAvailable(ReceiveBuffer.AsSpan(receivedByteCount, LengthPrefixSize - receivedByteCount));
				if (bytesRead == WaitingForPacket) {
					yield return WaitingForPacket;
				} else if (bytesRead == 0) {
					yield break;
				} else {
					receivedByteCount += bytesRead;
				}
			}

			int packetSize = BinaryPrimitives.ReadInt32LittleEndian(ReceiveBuffer.AsSpan(0, LengthPrefixSize));
			if (packetSize < 0 || packetSize > ReceiveBuffer.Length) {
				Dispose();
				yield break;
			}

			receivedByteCount = 0;
			while (receivedByteCount < packetSize) {
				int bytesRead = ReceiveAvailable(ReceiveBuffer.AsSpan(receivedByteCount, packetSize - receivedByteCount));
				if (bytesRead == WaitingForPacket) {
					yield return WaitingForPacket;
				} else if (bytesRead == 0) {
					yield break;
				} else {
					receivedByteCount += bytesRead;
				}
			}

			yield return packetSize;
		}
	}

	private void SendAll(ReadOnlySpan<byte> data) {
		try {
			while (!data.IsEmpty) {
				int bytesSent = Socket.Send(data, SocketFlags.None);
				if (bytesSent == 0) {
					Dispose();
					throw new SocketException();
				}

				data = data[bytesSent..];
			}
		} catch (SocketException) {
			Dispose();
			throw;
		} catch (ObjectDisposedException) {
			Dispose();
			throw;
		}
	}

	private int ReceiveAvailable(Span<byte> buffer) {
		try {
			// Read readiness also signals EOF, including when no bytes remain buffered.
			if (!Socket.Poll(0, SelectMode.SelectRead)) {
				return WaitingForPacket;
			}
			int bytesRead = Socket.Receive(buffer, SocketFlags.None);
			if (bytesRead == 0) {
				Dispose();
			}

			return bytesRead;
		} catch (SocketException exception) when (exception.SocketErrorCode == SocketError.WouldBlock) {
			return WaitingForPacket;
		} catch (SocketException) {
			Dispose();
			return 0;
		} catch (ObjectDisposedException) {
			Dispose();
			return 0;
		}
	}
}
