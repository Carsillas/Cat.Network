using System.Buffers.Binary;

namespace Cat.Network;

public class BufferWriter {
	private const int GuidSize = 16;
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

	public void WriteByte(byte value) {
		Span<byte> span = GetSpan(1);
		span[0] = value;
		Advance(1);
	}

	public void WriteBytes(ReadOnlySpan<byte> value) {
		Span<byte> span = GetSpan(value.Length);
		value.CopyTo(span);
		Advance(value.Length);
	}

	public void WriteUInt16(ushort value) {
		Span<byte> span = GetSpan(sizeof(ushort));
		BinaryPrimitives.WriteUInt16LittleEndian(span, value);
		Advance(sizeof(ushort));
	}

	public void WriteUInt16(Range range, ushort value) {
		BinaryPrimitives.WriteUInt16LittleEndian(GetSpan(range), value);
	}

	public void WriteInt16(short value) {
		Span<byte> span = GetSpan(sizeof(short));
		BinaryPrimitives.WriteInt16LittleEndian(span, value);
		Advance(sizeof(short));
	}

	public void WriteUInt32(uint value) {
		Span<byte> span = GetSpan(sizeof(uint));
		BinaryPrimitives.WriteUInt32LittleEndian(span, value);
		Advance(sizeof(uint));
	}

	public void WriteUInt32(Range range, uint value) {
		BinaryPrimitives.WriteUInt32LittleEndian(GetSpan(range), value);
	}

	public void WriteInt32(int value) {
		Span<byte> span = GetSpan(sizeof(int));
		BinaryPrimitives.WriteInt32LittleEndian(span, value);
		Advance(sizeof(int));
	}

	public void WriteInt32(Range range, int value) {
		BinaryPrimitives.WriteInt32LittleEndian(GetSpan(range), value);
	}

	public void WriteGuid(Guid value) {
		Span<byte> span = GetSpan(GuidSize);
		value.TryWriteBytes(span);
		Advance(GuidSize);
	}

	public void WriteInt64(long value) {
		Span<byte> span = GetSpan(sizeof(long));
		BinaryPrimitives.WriteInt64LittleEndian(span, value);
		Advance(sizeof(long));
	}

	public void WriteUInt64(ulong value) {
		Span<byte> span = GetSpan(sizeof(ulong));
		BinaryPrimitives.WriteUInt64LittleEndian(span, value);
		Advance(sizeof(ulong));
	}

	public void WriteSingle(float value) {
		Span<byte> span = GetSpan(sizeof(float));
		BinaryPrimitives.WriteSingleLittleEndian(span, value);
		Advance(sizeof(float));
	}

	public void WriteDouble(double value) {
		Span<byte> span = GetSpan(sizeof(double));
		BinaryPrimitives.WriteDoubleLittleEndian(span, value);
		Advance(sizeof(double));
	}

	public void WriteLengthPrefixedUtf8(string value) {
		Range lengthRange = Reserve(sizeof(uint));
		int start = WrittenCount;
		WriteUtf8(value);
		WriteUInt32(lengthRange, (uint)(WrittenCount - start));
	}

	public void WriteUtf8(string value) {
		int maxByteCount = System.Text.Encoding.UTF8.GetMaxByteCount(value.Length);
		Span<byte> span = GetSpan(maxByteCount);
		int written = System.Text.Encoding.UTF8.GetBytes(value, span);
		Advance(written);
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
