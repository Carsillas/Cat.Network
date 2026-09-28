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

	/// <summary>Appends a source field's encoded value under the destination name without decoding it.</summary>
	/// <remarks>
	/// Source names use ordinal comparison, and the last occurrence of a duplicate source name is copied.
	/// This does not remove source fields or replace fields already written. To rename a field, exclude its
	/// old name (and any destination field being replaced) from <see cref="CopyExcept"/> before copying it.
	/// The value bytes are preserved exactly; element types and collection encodings are not converted.
	/// </remarks>
	/// <exception cref="ArgumentException">Either name is null, empty, or whitespace.</exception>
	/// <exception cref="KeyNotFoundException">The source reader does not contain the source field.</exception>
	/// <exception cref="InvalidOperationException">
	/// There is no source reader, the writer is completed, or the field count limit has been reached.
	/// </exception>
	public void Copy(string sourceName, string destinationName) {
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
		ArgumentException.ThrowIfNullOrWhiteSpace(destinationName);
		if (reader is null) {
			throw new InvalidOperationException("Copy requires a source upgrade reader.");
		}

		NetworkObjectUpgradeField field = reader.GetField(sourceName);
		WriteRaw(destinationName, field.Value.Span);
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
