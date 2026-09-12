namespace Cat.Network.Test;

public sealed class BufferWriterCapacityTests {
	[Test]
	public void Constructor_RejectsCapacityBeyondArrayLimit() {
		Assert.Throws<ArgumentOutOfRangeException>(() => new BufferWriter(Array.MaxLength + 1));
		Assert.Throws<ArgumentOutOfRangeException>(() => new BufferWriter(int.MaxValue));
	}

	[Test]
	public void GetSpan_RejectsIntMaxValueWhenEmpty() {
		BufferWriter writer = CreateWriter(0);

		AssertRejectedWithoutMutation<OutOfMemoryException>(writer, () => writer.GetSpan(int.MaxValue));
	}

	[TestCase(1)]
	[TestCase(16)]
	public void GetSpan_RejectsIntMaxValueWithWrittenBytes(int writtenCount) {
		BufferWriter writer = CreateWriter(writtenCount);

		AssertRejectedWithoutMutation<OutOfMemoryException>(writer, () => writer.GetSpan(int.MaxValue));
	}

	[Test]
	public void Reserve_RejectsIntMaxValueWhenEmpty() {
		BufferWriter writer = CreateWriter(0);

		AssertRejectedWithoutMutation<OutOfMemoryException>(writer, () => writer.Reserve(int.MaxValue));
	}

	[TestCase(1)]
	[TestCase(16)]
	public void Reserve_RejectsIntMaxValueWithWrittenBytes(int writtenCount) {
		BufferWriter writer = CreateWriter(writtenCount);

		AssertRejectedWithoutMutation<OutOfMemoryException>(writer, () => writer.Reserve(int.MaxValue));
	}

	[TestCase(1)]
	[TestCase(16)]
	public void GetSpan_RejectsTotalBeyondArrayLimitWithoutChangingWriter(int writtenCount) {
		BufferWriter writer = CreateWriter(writtenCount);
		int request = Array.MaxLength - writtenCount + 1;

		AssertRejectedWithoutMutation<OutOfMemoryException>(writer, () => writer.GetSpan(request));
	}

	[TestCase(1)]
	[TestCase(16)]
	public void Reserve_RejectsTotalBeyondArrayLimitWithoutChangingWriter(int writtenCount) {
		BufferWriter writer = CreateWriter(writtenCount);
		int request = Array.MaxLength - writtenCount + 1;

		AssertRejectedWithoutMutation<OutOfMemoryException>(writer, () => writer.Reserve(request));
	}

	[TestCase(-1)]
	[TestCase(int.MinValue)]
	public void GetSpan_RejectsNegativeHintWithoutChangingWriter(int count) {
		BufferWriter writer = CreateWriter(16);

		AssertRejectedWithoutMutation<ArgumentOutOfRangeException>(writer, () => writer.GetSpan(count));
	}

	[TestCase(-1)]
	[TestCase(int.MinValue)]
	public void Reserve_RejectsNegativeCountWithoutChangingWriter(int count) {
		BufferWriter writer = CreateWriter(16);

		AssertRejectedWithoutMutation<ArgumentOutOfRangeException>(writer, () => writer.Reserve(count));
	}

	[TestCase(-1)]
	[TestCase(int.MinValue)]
	public void Advance_RejectsNegativeCountWithoutChangingWriter(int count) {
		BufferWriter writer = CreateWriter(8);

		AssertRejectedWithoutMutation<ArgumentOutOfRangeException>(writer, () => writer.Advance(count));
	}

	[TestCase(9)]
	[TestCase(int.MaxValue)]
	public void Advance_RejectsCountBeyondFreeCapacityWithoutChangingWriter(int count) {
		BufferWriter writer = CreateWriter(8);

		AssertRejectedWithoutMutation<InvalidOperationException>(writer, () => writer.Advance(count));
	}

	[TestCase(0)]
	[TestCase(1)]
	[TestCase(16)]
	public void GetSpan_ZeroHintReturnsWritableSpaceWithoutAdvancing(int writtenCount) {
		BufferWriter writer = CreateWriter(writtenCount);
		byte[] prefix = writer.GetWrittenSpan().ToArray();

		Span<byte> span = writer.GetSpan(0);

		Assert.That(span.Length, Is.GreaterThan(0));
		Assert.That(writer.WrittenCount, Is.EqualTo(writtenCount));
		Assert.That(writer.GetWrittenSpan().ToArray(), Is.EqualTo(prefix));
		span[0] = 42;
		writer.Advance(1);
		Assert.That(writer.GetWrittenSpan().ToArray(), Is.EqualTo(prefix.Append((byte)42).ToArray()));
	}

	[TestCase(0)]
	[TestCase(1)]
	[TestCase(16)]
	public void Reserve_ZeroCountReturnsEmptyRangeWithoutAdvancing(int writtenCount) {
		BufferWriter writer = CreateWriter(writtenCount);
		byte[] prefix = writer.GetWrittenSpan().ToArray();

		Range range = writer.Reserve(0);

		Assert.That(range, Is.EqualTo(writtenCount..writtenCount));
		Assert.That(writer.WrittenCount, Is.EqualTo(writtenCount));
		Assert.That(writer.GetWrittenSpan().ToArray(), Is.EqualTo(prefix));
	}

	[TestCase(1, 2)]
	[TestCase(3, 20)]
	[TestCase(16, 40)]
	public void GetSpan_ExpansionPreservesWrittenBytesAndSatisfiesHint(int initialCapacity, int request) {
		BufferWriter writer = new(initialCapacity);
		byte[] prefix = Enumerable.Range(1, initialCapacity).Select(value => (byte)value).ToArray();
		writer.WriteBytes(prefix);

		Span<byte> span = writer.GetSpan(request);

		Assert.That(span.Length, Is.GreaterThanOrEqualTo(request));
		Assert.That(writer.WrittenCount, Is.EqualTo(prefix.Length));
		span[..request].Fill(42);
		writer.Advance(request);
		Assert.That(writer.GetWrittenSpan().ToArray(), Is.EqualTo(prefix.Concat(Enumerable.Repeat((byte)42, request)).ToArray()));
	}

	[Test]
	public void Reserve_ExpansionPreservesEarlierRangesAndWrittenBytes() {
		BufferWriter writer = new(4);
		Range lengthRange = writer.Reserve(sizeof(int));
		writer.WriteByte(11);

		Range payloadRange = writer.Reserve(20);
		writer.GetSpan(payloadRange).Fill(42);
		writer.WriteInt32(lengthRange, 21);
		writer.WriteByte(99);

		byte[] expected = new byte[] { 21, 0, 0, 0, 11 }
			.Concat(Enumerable.Repeat((byte)42, 20)).Append((byte)99).ToArray();
		Assert.That(payloadRange, Is.EqualTo(5..25));
		Assert.That(writer.WrittenCount, Is.EqualTo(expected.Length));
		Assert.That(writer.GetWrittenSpan().ToArray(), Is.EqualTo(expected));
	}

	[Test]
	public void Advance_AcceptsZeroAndExactlyRemainingCapacity() {
		BufferWriter writer = CreateWriter(8);
		byte[] prefix = writer.GetWrittenSpan().ToArray();
		writer.GetSpan(8).Fill(42);

		writer.Advance(0);
		Assert.That(writer.WrittenCount, Is.EqualTo(8));
		writer.Advance(8);

		Assert.That(writer.FreeCapacity, Is.Zero);
		Assert.That(writer.GetWrittenSpan().ToArray(), Is.EqualTo(prefix.Concat(Enumerable.Repeat((byte)42, 8)).ToArray()));
	}

	private static BufferWriter CreateWriter(int writtenCount) {
		BufferWriter writer = new(16);
		writer.WriteBytes(Enumerable.Range(1, writtenCount).Select(value => (byte)value).ToArray());
		return writer;
	}

	private static void AssertRejectedWithoutMutation<TException>(BufferWriter writer, TestDelegate request) where TException : Exception {
		byte[] prefix = writer.GetWrittenSpan().ToArray();
		int freeCapacity = writer.FreeCapacity;

		Assert.Throws<TException>(request);

		Assert.That(writer.WrittenCount, Is.EqualTo(prefix.Length));
		Assert.That(writer.FreeCapacity, Is.EqualTo(freeCapacity));
		Assert.That(writer.GetWrittenSpan().ToArray(), Is.EqualTo(prefix));
	}
}
