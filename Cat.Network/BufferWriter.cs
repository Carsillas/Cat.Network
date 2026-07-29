namespace Cat.Network;

public class BufferWriter {
	private byte[] Buffer { get; set; } = new byte[16384];

	public int WrittenCount { get; private set; }

	public void Clear() {
		WrittenCount = 0;
	}

	public void Advance(int writtenCount) {
		if (writtenCount < 0) {
			throw new ArgumentOutOfRangeException(nameof(writtenCount));
		}

		if (WrittenCount > Buffer.Length - writtenCount) {
			throw new InvalidOperationException("Cannot advance past the end of the buffer.");
		}

		WrittenCount += writtenCount;
	}

	public ReadOnlySpan<byte> GetWrittenSpan() {
		return Buffer.AsSpan(0, WrittenCount);
	}

	public Span<byte> GetSpan(int minByteCount) {
		EnsureFreeCapacity(minByteCount);
		return Buffer.AsSpan(WrittenCount);
	}

	public Span<byte> GetSpan(Range range) {
		return Buffer.AsSpan(range);
	}

	public Range Reserve(int count) {
		EnsureFreeCapacity(count);
		int start = WrittenCount;
		Advance(count);
		return start..WrittenCount;
	}

	private void EnsureFreeCapacity(int byteCount) {
		if (byteCount <= 0) {
			byteCount = 1;
		}

		int availableCapacity = Buffer.Length - WrittenCount;
		if (availableCapacity >= byteCount) {
			return;
		}

		int requiredCapacity = WrittenCount + byteCount;
		int newCapacity = Buffer.Length;
		while (newCapacity < requiredCapacity) {
			newCapacity *= 2;
		}

		byte[] newBuffer = new byte[newCapacity];
		System.Buffer.BlockCopy(Buffer, 0, newBuffer, 0, WrittenCount);
		Buffer = newBuffer;
	}
}
