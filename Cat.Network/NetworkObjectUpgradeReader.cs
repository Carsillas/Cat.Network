using System.Buffers.Binary;

namespace Cat.Network;

public sealed class NetworkObjectUpgradeReader {
	private readonly Dictionary<string, NetworkObjectUpgradeField> fieldsByName;
	private readonly SerializationContext context;

	private NetworkObjectUpgradeReader(IReadOnlyList<NetworkObjectUpgradeField> fields, SerializationContext context) {
		Fields = fields;
		this.context = context;
		fieldsByName = new Dictionary<string, NetworkObjectUpgradeField>(StringComparer.Ordinal);
		foreach (NetworkObjectUpgradeField field in fields) {
			fieldsByName[field.Name] = field;
		}
	}

	public IReadOnlyList<NetworkObjectUpgradeField> Fields { get; }

	public T Get<T>(string name) {
		NetworkObjectUpgradeField field = GetField(name);
		return NetworkObjectUpgradeCodec.Deserialize<T>(field.Value.Span, context);
	}

	public bool TryGet<T>(string name, out T value) {
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		if (!fieldsByName.TryGetValue(name, out NetworkObjectUpgradeField field)) {
			value = default!;
			return false;
		}

		value = NetworkObjectUpgradeCodec.Deserialize<T>(field.Value.Span, context);
		return true;
	}

	internal NetworkObjectUpgradeField GetField(string name) {
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		if (!fieldsByName.TryGetValue(name, out NetworkObjectUpgradeField field)) {
			throw new KeyNotFoundException($"Upgrade payload does not contain a field named '{name}'.");
		}

		return field;
	}

	public static bool TryCreate(ReadOnlySpan<byte> data, SerializationContext context, out NetworkObjectUpgradeReader reader) {
		reader = null!;
		if (data.Length < 3) {
			return false;
		}

		MemberIdentificationMode memberIdentificationMode = (MemberIdentificationMode)data[0];
		if (memberIdentificationMode != MemberIdentificationMode.Name) {
			return false;
		}

		data = data[1..];
		ushort fieldCount = BinaryPrimitives.ReadUInt16LittleEndian(data);
		data = data[2..];

		List<NetworkObjectUpgradeField> fields = new(fieldCount);
		for (int fieldIndex = 0; fieldIndex < fieldCount; fieldIndex++) {
			if (data.Length < 4) {
				return false;
			}

			uint nameByteCount = BinaryPrimitives.ReadUInt32LittleEndian(data);
			data = data[4..];
			if (data.Length < nameByteCount) {
				return false;
			}

			string name = System.Text.Encoding.UTF8.GetString(data[..(int)nameByteCount]);
			data = data[(int)nameByteCount..];

			if (data.Length < 4) {
				return false;
			}

			uint valueByteCount = BinaryPrimitives.ReadUInt32LittleEndian(data);
			data = data[4..];
			if (data.Length < valueByteCount) {
				return false;
			}

			fields.Add(new NetworkObjectUpgradeField(name, data[..(int)valueByteCount].ToArray()));
			data = data[(int)valueByteCount..];
		}

		if (!data.IsEmpty) {
			return false;
		}

		reader = new NetworkObjectUpgradeReader(fields, context);
		return true;
	}
}
