using System.Buffers.Binary;
using System.Net.Sockets;

namespace Cat.Network;

public sealed class SocketTransport : IRelayTransport, IDisposable {
	private const int LengthPrefixSize = sizeof(int);
	private const uint DefaultMaxPacketSize = 1_048_576;
	private const int WaitingForPacket = -1;

	private Socket Socket { get; }
	private int MaxPacketSize { get; }
	private byte[] ReceiveBuffer { get; }
	private IEnumerator<int> ReceiveEnumerator { get; }
	private bool Disposed { get; set; }

	public SocketTransport(Socket socket, uint maxPacketSize = DefaultMaxPacketSize) {
		ArgumentNullException.ThrowIfNull(socket);
		if (maxPacketSize == 0 || maxPacketSize > int.MaxValue) {
			throw new ArgumentOutOfRangeException(nameof(maxPacketSize));
		}

		Socket = socket;
		MaxPacketSize = (int)maxPacketSize;
		ReceiveBuffer = new byte[Math.Max(LengthPrefixSize, MaxPacketSize)];
		ReceiveEnumerator = ReadAvailablePackets();
	}

	public event MessageHandler? MessageReceived;
	public event Action<IRelayTransport>? Disconnected;

	public void Send(ReadOnlySpan<byte> message) {
		ObjectDisposedException.ThrowIf(Disposed, this);
		if (message.Length > MaxPacketSize) {
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
			while (Socket.Available < LengthPrefixSize) {
				yield return WaitingForPacket;
			}

			if (!TryReceiveAll(ReceiveBuffer.AsSpan(0, LengthPrefixSize))) {
				yield break;
			}

			int packetSize = BinaryPrimitives.ReadInt32LittleEndian(ReceiveBuffer.AsSpan(0, LengthPrefixSize));
			if (packetSize < 0 || packetSize > MaxPacketSize) {
				Dispose();
				yield break;
			}

			while (Socket.Available < packetSize) {
				yield return WaitingForPacket;
			}

			if (!TryReceiveAll(ReceiveBuffer.AsSpan(0, packetSize))) {
				yield break;
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

	private bool TryReceiveAll(Span<byte> buffer) {
		try {
			while (!buffer.IsEmpty) {
				int bytesRead = Socket.Receive(buffer, SocketFlags.None);
				if (bytesRead == 0) {
					Dispose();
					return false;
				}

				buffer = buffer[bytesRead..];
			}

			return true;
		} catch (SocketException) {
			Dispose();
			return false;
		} catch (ObjectDisposedException) {
			Dispose();
			return false;
		}
	}
}
