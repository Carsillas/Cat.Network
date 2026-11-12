using System.Buffers.Binary;

namespace Cat.Network;

internal ref struct NetworkCollectionReader(ReadOnlySpan<byte> data) {
	private ReadOnlySpan<byte> remaining = data;

	public int ReadOperationCount() {
		int count = ReadInt32();
		if (count < 0) {
			throw new InvalidOperationException("Collection operation count is negative.");
		}

		return count;
	}

	public NetworkCollectionOperationType ReadOperationType() {
		if (remaining.IsEmpty) {
			throw new InvalidOperationException("Collection operation is missing.");
		}

		NetworkCollectionOperationType type = (NetworkCollectionOperationType)remaining[0];
		remaining = remaining[1..];
		return type;
	}

	public int ReadInt32() {
		if (remaining.Length < 4) {
			throw new InvalidOperationException("Collection integer payload is truncated.");
		}

		int value = BinaryPrimitives.ReadInt32LittleEndian(remaining);
		remaining = remaining[4..];
		return value;
	}

	public ReadOnlySpan<byte> ReadPayload() {
		int length = ReadInt32();
		if (length < 0 || length > remaining.Length) {
			throw new InvalidOperationException("Collection item payload length is invalid or truncated.");
		}

		ReadOnlySpan<byte> payload = remaining[..length];
		remaining = remaining[length..];
		return payload;
	}

	public void EnsureFullyConsumed() {
		if (!remaining.IsEmpty) {
			throw new InvalidOperationException("Collection payload was not fully consumed.");
		}
	}
}
