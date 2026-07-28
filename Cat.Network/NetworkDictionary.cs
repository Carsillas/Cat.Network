using System.Collections;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace Cat.Network;

public abstract class NetworkDictionary<TKey, TValue> : IDictionary<TKey, TValue>, INetworkCollection where TKey : notnull {
	private static NetworkCollectionSerializer.ItemCodec KeyCodec { get; } = NetworkCollectionSerializer.GetCodec<TKey>();
	private static NetworkCollectionSerializer.ItemCodec ValueCodec { get; } = NetworkCollectionSerializer.GetCodec<TValue>();

	protected NetworkDictionary() {
		if (KeyCodec.IsNetworkObject) {
			throw new InvalidOperationException("Network dictionary keys cannot be NetworkObjects.");
		}
	}

	protected NetworkObject Owner { get; private set; } = null!;

	protected int PropertyIndex { get; private set; } = -1;

	protected Dictionary<TKey, TValue> Items { get; } = [];

	private List<NetworkDictionaryOperation<TKey, TValue>> OperationBuffer { get; } = [];

	public TValue this[TKey key] {
		get => Items[key];
		set {
			if (Items.TryGetValue(key, out TValue? previous) && EqualityComparer<TValue>.Default.Equals(previous, value)) {
				return;
			}

			ValidateValueForAssignment(value);
			if (Items.TryGetValue(key, out previous)) {
				OnValueRemoving(previous);
			}

			Items[key] = value;
			OnValueAdded(value);
			OperationBuffer.Add(new NetworkDictionaryOperation<TKey, TValue>(NetworkCollectionOperationType.Set, key, value));
			MarkOwnerModified();
		}
	}

	public ICollection<TKey> Keys => Items.Keys;

	public ICollection<TValue> Values => Items.Values;

	public int Count => Items.Count;

	public bool IsReadOnly => false;

	public void Add(TKey key, TValue value) {
		ValidateValueForAssignment(value);
		Items.Add(key, value);
		OnValueAdded(value);
		OperationBuffer.Add(new NetworkDictionaryOperation<TKey, TValue>(NetworkCollectionOperationType.Add, key, value));
		MarkOwnerModified();
	}

	public bool ContainsKey(TKey key) {
		return Items.ContainsKey(key);
	}

	public bool Remove(TKey key) {
		if (!Items.TryGetValue(key, out TValue? value)) {
			return false;
		}

		OnValueRemoving(value);
		Items.Remove(key);
		OperationBuffer.Add(new NetworkDictionaryOperation<TKey, TValue>(NetworkCollectionOperationType.Remove, key));
		MarkOwnerModified();
		return true;
	}

	public bool TryGetValue(TKey key, out TValue value) {
		return Items.TryGetValue(key, out value!);
	}

	public void Add(KeyValuePair<TKey, TValue> item) {
		Add(item.Key, item.Value);
	}

	public void Clear() {
		if (Items.Count == 0) {
			return;
		}

		foreach (TValue value in Items.Values) {
			OnValueRemoving(value);
		}

		Items.Clear();
		OperationBuffer.Add(new NetworkDictionaryOperation<TKey, TValue>(NetworkCollectionOperationType.Clear, default!));
		MarkOwnerModified();
	}

	public bool Contains(KeyValuePair<TKey, TValue> item) {
		return ((ICollection<KeyValuePair<TKey, TValue>>)Items).Contains(item);
	}

	public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
		((ICollection<KeyValuePair<TKey, TValue>>)Items).CopyTo(array, arrayIndex);
	}

	public bool Remove(KeyValuePair<TKey, TValue> item) {
		if (!((ICollection<KeyValuePair<TKey, TValue>>)Items).Contains(item)) {
			return false;
		}

		return Remove(item.Key);
	}

	public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
		return Items.GetEnumerator();
	}

	public void Initialize(NetworkObject owner, int propertyIndex) {
		if (Owner is not null) {
			if (ReferenceEquals(Owner, owner) && PropertyIndex == propertyIndex) {
				return;
			}

			throw new InvalidOperationException("Network collection is already initialized.");
		}

		Owner = owner;
		PropertyIndex = propertyIndex;
	}

	public void Serialize(BufferWriter writer, SerializationContext context, SerializationOptions options) {
		Range operationCountRange = writer.Reserve(4);
		int operationCount = 0;

		if (options.MemberSelectionMode == MemberSelectionMode.All) {
			WriteOperationType(writer, NetworkCollectionOperationType.Clear);
			operationCount++;

			foreach ((TKey key, TValue value) in Items) {
				WriteOperationType(writer, NetworkCollectionOperationType.Add);
				WriteKey(writer, key, context, options);
				WriteValue(writer, value, context, options, update: false);
				operationCount++;
			}
		} else {
			HashSet<TKey> touchedKeys = [];
			foreach (NetworkDictionaryOperation<TKey, TValue> operation in OperationBuffer) {
				WriteOperation(writer, operation, context, options);
				operationCount++;
				if (operation.OperationType != NetworkCollectionOperationType.Clear) {
					touchedKeys.Add(operation.Key);
				}
			}

			if (ValueCodec.IsNetworkObject) {
				foreach ((TKey key, TValue value) in Items) {
					if (touchedKeys.Contains(key) || value is not NetworkObject item || !NetworkCollectionSerializer.HasDirtyState(item)) {
						continue;
					}

					WriteOperationType(writer, NetworkCollectionOperationType.Update);
					WriteKey(writer, key, context, options);
					WriteValue(writer, value, context, options, update: true);
					operationCount++;
				}
			}
		}

		BinaryPrimitives.WriteInt32LittleEndian(writer.GetSpan(operationCountRange), operationCount);
	}

	public void Deserialize(ReadOnlySpan<byte> data, SerializationContext context) {
		if (data.Length < 4) {
			return;
		}

		int offset = 0;
		int operationCount = ReadInt32(data, ref offset);

		for (int operationIndex = 0; operationIndex < operationCount; operationIndex++) {
			if (data.Length - offset < 1) {
				return;
			}

			NetworkCollectionOperationType operationType = (NetworkCollectionOperationType)data[offset];
			offset++;

			switch (operationType) {
				case NetworkCollectionOperationType.Add: {
					TKey key = ReadKey(data, ref offset, context);
					TValue value = ReadValue(data, ref offset, context);
					AddDeserialized(key, value);
					break;
				}
				case NetworkCollectionOperationType.Remove: {
					TKey key = ReadKey(data, ref offset, context);
					RemoveDeserialized(key);
					break;
				}
				case NetworkCollectionOperationType.Set: {
					TKey key = ReadKey(data, ref offset, context);
					TValue value = ReadValue(data, ref offset, context);
					SetDeserialized(key, value);
					break;
				}
				case NetworkCollectionOperationType.Clear:
					ClearDeserialized();
					break;
				case NetworkCollectionOperationType.Update: {
					TKey key = ReadKey(data, ref offset, context);
					int valueLength = ReadInt32(data, ref offset);
					if (data.Length - offset < valueLength) {
						return;
					}

					if (Items.TryGetValue(key, out TValue? value)) {
						ValueCodec.DeserializeUpdate(value, data.Slice(offset, valueLength), context);
					}

					offset += valueLength;
					break;
				}
				default:
					return;
			}
		}
	}

	IEnumerator IEnumerable.GetEnumerator() {
		return GetEnumerator();
	}

	protected virtual void ValidateValueForAssignment(TValue value) {
	}

	protected virtual void OnValueAdded(TValue value) {
	}

	protected virtual void OnValueRemoving(TValue value) {
	}

	protected void AddDeserialized(TKey key, TValue value) {
		ValidateValueForAssignment(value);
		Items.Add(key, value);
		OnValueAdded(value);
	}

	protected void SetDeserialized(TKey key, TValue value) {
		if (Items.TryGetValue(key, out TValue? previous)) {
			OnValueRemoving(previous);
		}

		ValidateValueForAssignment(value);
		Items[key] = value;
		OnValueAdded(value);
	}

	protected void RemoveDeserialized(TKey key) {
		if (!Items.TryGetValue(key, out TValue? value)) {
			return;
		}

		OnValueRemoving(value);
		Items.Remove(key);
	}

	protected void ClearDeserialized() {
		foreach (TValue value in Items.Values) {
			OnValueRemoving(value);
		}

		Items.Clear();
	}

	protected void MarkOwnerModified() {
		INetworkObject current = Owner;
		current.PropertyStates[PropertyIndex] |= NetworkPropertyState.Modified;

		while (current.Parent is NetworkObject parent) {
			INetworkObject parentObject = parent;
			parentObject.PropertyStates[current.PropertyIndex] |= NetworkPropertyState.Modified;
			current = parentObject;
		}
	}

	private void WriteOperation(BufferWriter writer, NetworkDictionaryOperation<TKey, TValue> operation, SerializationContext context, SerializationOptions options) {
		WriteOperationType(writer, operation.OperationType);
		switch (operation.OperationType) {
			case NetworkCollectionOperationType.Add:
			case NetworkCollectionOperationType.Set:
				WriteKey(writer, operation.Key, context, options);
				WriteValue(writer, operation.Value!, context, options, update: false);
				break;
			case NetworkCollectionOperationType.Remove:
				WriteKey(writer, operation.Key, context, options);
				break;
			case NetworkCollectionOperationType.Clear:
				break;
			case NetworkCollectionOperationType.Update:
				WriteKey(writer, operation.Key, context, options);
				WriteValue(writer, operation.Value!, context, options, update: true);
				break;
			default:
				throw new InvalidOperationException($"Unsupported dictionary operation '{operation.OperationType}'.");
		}
	}

	private static TKey ReadKey(ReadOnlySpan<byte> data, ref int offset, SerializationContext context) {
		int keyLength = ReadInt32(data, ref offset);
		if (data.Length - offset < keyLength) {
			throw new InvalidOperationException("Dictionary key payload is truncated.");
		}

		TKey key = KeyCodec.DeserializeFull<TKey>(data.Slice(offset, keyLength), context);
		offset += keyLength;
		return key;
	}

	private static TValue ReadValue(ReadOnlySpan<byte> data, ref int offset, SerializationContext context) {
		int valueLength = ReadInt32(data, ref offset);
		if (data.Length - offset < valueLength) {
			throw new InvalidOperationException("Dictionary value payload is truncated.");
		}

		TValue value = ValueCodec.DeserializeFull<TValue>(data.Slice(offset, valueLength), context);
		offset += valueLength;
		return value;
	}

	private static void WriteKey(BufferWriter writer, TKey key, SerializationContext context, SerializationOptions options) {
		Range keyLengthRange = writer.Reserve(4);
		int keyStart = writer.WrittenCount;
		KeyCodec.SerializeFull(writer, key, context, options);
		BinaryPrimitives.WriteInt32LittleEndian(writer.GetSpan(keyLengthRange), writer.WrittenCount - keyStart);
	}

	private static void WriteValue(BufferWriter writer, TValue value, SerializationContext context, SerializationOptions options, bool update) {
		Range valueLengthRange = writer.Reserve(4);
		int valueStart = writer.WrittenCount;
		if (update) {
			ValueCodec.SerializeUpdate(writer, value, context, options);
		} else {
			ValueCodec.SerializeFull(writer, value, context, options);
		}

		BinaryPrimitives.WriteInt32LittleEndian(writer.GetSpan(valueLengthRange), writer.WrittenCount - valueStart);
	}

	private static int ReadInt32(ReadOnlySpan<byte> data, ref int offset) {
		int value = BinaryPrimitives.ReadInt32LittleEndian(data[offset..]);
		offset += 4;
		return value;
	}

	private static void WriteOperationType(BufferWriter writer, NetworkCollectionOperationType operationType) {
		Span<byte> span = writer.GetSpan(1);
		span[0] = (byte)operationType;
		writer.Advance(1);
	}

	private readonly record struct NetworkDictionaryOperation<TStoredKey, TStoredValue>(NetworkCollectionOperationType OperationType, TStoredKey Key, TStoredValue? Value = default) where TStoredKey : notnull;
}
