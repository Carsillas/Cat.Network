using System.Buffers.Binary;

namespace Cat.Network;

public sealed class NetworkObjectUpgradeWriter {
	private readonly BufferWriter writer;
	private readonly NetworkObjectUpgradeReader? reader;
	private readonly SerializationContext context;
	private readonly Range fieldCountRange;
	private ushort fieldCount;
	private bool completed;

	public NetworkObjectUpgradeWriter(BufferWriter writer, NetworkObjectUpgradeReader? reader, SerializationContext context) {
		this.writer = writer;
		this.reader = reader;
		this.context = context;
		WriteByte((byte)MemberIdentificationMode.Name);
		fieldCountRange = writer.Reserve(2);
	}

	public void CopyExcept(params string[] names) {
		if (reader is null) {
			throw new InvalidOperationException("CopyExcept requires a source upgrade reader.");
		}

		HashSet<string> excludedNames = new(names, StringComparer.Ordinal);
		foreach (NetworkObjectUpgradeField field in reader.Fields) {
			if (!excludedNames.Contains(field.Name)) {
				WriteRaw(field.Name, field.Value.Span);
			}
		}
	}

	public void Write<T>(string name, T value) {
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		BufferWriter valueWriter = new();
		NetworkObjectUpgradeCodec.Serialize(valueWriter, value, context);
		WriteRaw(name, valueWriter.GetWrittenSpan());
	}

	public byte[] Complete() {
		if (completed) {
			return writer.GetWrittenSpan().ToArray();
		}

		BinaryPrimitives.WriteUInt16LittleEndian(writer.GetSpan(fieldCountRange), fieldCount);
		completed = true;
		return writer.GetWrittenSpan().ToArray();
	}

	internal void WriteRaw(string name, ReadOnlySpan<byte> value) {
		if (completed) {
			throw new InvalidOperationException("Cannot write after the upgrade payload has been completed.");
		}
		if (fieldCount == ushort.MaxValue) {
			throw new InvalidOperationException("Cannot write more than 65535 fields.");
		}

		byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(name);
		WriteUInt32((uint)nameBytes.Length);
		WriteBytes(nameBytes);
		WriteUInt32((uint)value.Length);
		WriteBytes(value);
		fieldCount++;
	}

	private void WriteByte(byte value) {
		Span<byte> span = writer.GetSpan(1);
		span[0] = value;
		writer.Advance(1);
	}

	private void WriteUInt32(uint value) {
		Span<byte> span = writer.GetSpan(4);
		BinaryPrimitives.WriteUInt32LittleEndian(span, value);
		writer.Advance(4);
	}

	private void WriteBytes(ReadOnlySpan<byte> value) {
		Span<byte> span = writer.GetSpan(value.Length);
		value.CopyTo(span);
		writer.Advance(value.Length);
	}
}
