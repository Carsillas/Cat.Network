namespace Cat.Network.Test;

public sealed class BufferWriterTests {
	[Test]
	public void Constructor_UsesInitialBufferSize() {
		BufferWriter writer = new(32);

		Assert.That(writer.FreeCapacity, Is.EqualTo(32));
	}

	[Test]
	public void Constructor_RejectsNonPositiveInitialBufferSize() {
		Assert.Throws<ArgumentOutOfRangeException>(() => new BufferWriter(0));
		Assert.Throws<ArgumentOutOfRangeException>(() => new BufferWriter(-1));
	}

	[Test]
	public void FreeCapacity_TracksSpaceAvailableInCurrentBuffer() {
		BufferWriter writer = new();
		int initialFreeCapacity = writer.FreeCapacity;

		writer.WriteByte(42);

		Assert.That(writer.FreeCapacity, Is.EqualTo(initialFreeCapacity - 1));
	}

	[Test]
	public void FreeCapacity_TracksExpandedBufferSpace() {
		BufferWriter writer = new();
		int initialFreeCapacity = writer.FreeCapacity;

		writer.WriteBytes(new byte[initialFreeCapacity + 1]);

		Assert.That(writer.FreeCapacity, Is.EqualTo(initialFreeCapacity - 1));
	}

	[Test]
	public void FreeCapacity_WritingExactCountDoesNotExpandBuffer() {
		BufferWriter writer = new();
		int initialFreeCapacity = writer.FreeCapacity;

		writer.WriteBytes(new byte[initialFreeCapacity]);

		Assert.That(writer.FreeCapacity, Is.EqualTo(0));
	}
}
