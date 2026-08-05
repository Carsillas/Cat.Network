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
		writer.WriteByte((byte)MemberIdentificationMode.Name);
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

		writer.WriteUInt16(fieldCountRange, fieldCount);
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

		writer.WriteLengthPrefixedUtf8(name);
		writer.WriteUInt32((uint)value.Length);
		writer.WriteBytes(value);
		fieldCount++;
	}
}
