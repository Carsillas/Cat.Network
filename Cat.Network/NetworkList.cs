using System.Collections;
using System.Collections.Generic;

namespace Cat.Network;

public abstract class NetworkList<T> : IList<T>, INetworkCollection {
	private static NetworkCollectionSerializer.ItemCodec Codec { get; } = NetworkCollectionSerializer.GetCodec<T>();

	internal NetworkList() { }

	public delegate void CollectionChangedEvent(NetworkList<T> sender, int index);

	public event CollectionChangedEvent? ItemAdded;

	public event CollectionChangedEvent? ItemRemoved;

	public event CollectionChangedEvent? IndexChanged;

	public NetworkObject Owner { get; private set; } = null!;

	protected int PropertyIndex { get; private set; } = -1;

	protected List<T> Items { get; } = [];

	protected List<NetworkCollectionOperation<T>> OperationBuffer { get; } = [];

	public int Count => Items.Count;

	public bool IsReadOnly => false;

	public T this[int index] {
		get => Items[index];
		set {
			T previous = Items[index];
			if (EqualityComparer<T>.Default.Equals(previous, value)) {
				return;
			}

			ValidateItemForAssignment(value);
			OnItemRemoving(previous);
			Items[index] = value;
			OnItemAdded(value);
			OperationBuffer.Add(new NetworkCollectionOperation<T>(NetworkCollectionOperationType.Set, index, value));
			MarkOwnerModified();
			IndexChanged?.Invoke(this, index);
		}
	}

	public void Add(T item) {
		ValidateItemForAssignment(item);
		Items.Add(item);
		OnItemAdded(item);
		OperationBuffer.Add(new NetworkCollectionOperation<T>(NetworkCollectionOperationType.Add, Items.Count - 1, item));
		MarkOwnerModified();
		ItemAdded?.Invoke(this, Items.Count - 1);
	}

	public void Clear() {
		if (Items.Count == 0) {
			return;
		}

		for (int index = Items.Count - 1; index >= 0; index--) {
			OnItemRemoving(Items[index]);
		}

		int removedCount = Items.Count;
		Items.Clear();
		OperationBuffer.Add(new NetworkCollectionOperation<T>(NetworkCollectionOperationType.Clear));
		MarkOwnerModified();
		for (int index = removedCount - 1; index >= 0; index--) {
			ItemRemoved?.Invoke(this, index);
		}
	}

	public bool Contains(T item) {
		return Items.Contains(item);
	}

	public void CopyTo(T[] array, int arrayIndex) {
		Items.CopyTo(array, arrayIndex);
	}

	public IEnumerator<T> GetEnumerator() {
		return Items.GetEnumerator();
	}

	public int IndexOf(T item) {
		return Items.IndexOf(item);
	}

	public void Insert(int index, T item) {
		ValidateItemForAssignment(item);
		Items.Insert(index, item);
		OnItemAdded(item);
		OperationBuffer.Add(new NetworkCollectionOperation<T>(NetworkCollectionOperationType.Insert, index, item));
		MarkOwnerModified();
		ItemAdded?.Invoke(this, index);
	}

	public bool Remove(T item) {
		int index = Items.IndexOf(item);
		if (index < 0) {
			return false;
		}

		RemoveAt(index);
		return true;
	}

	public void RemoveAt(int index) {
		T item = Items[index];
		OnItemRemoving(item);
		Items.RemoveAt(index);
		OperationBuffer.Add(new NetworkCollectionOperation<T>(NetworkCollectionOperationType.Remove, index));
		MarkOwnerModified();
		ItemRemoved?.Invoke(this, index);
	}

	void INetworkCollection.Initialize(NetworkObject owner, int propertyIndex) {
		if (Owner is not null) {
			if (ReferenceEquals(Owner, owner) && PropertyIndex == propertyIndex) {
				return;
			}

			throw new InvalidOperationException("Network collection is already initialized.");
		}

		Owner = owner;
		PropertyIndex = propertyIndex;
	}

	void INetworkCollection.Serialize(BufferWriter writer, SerializationContext context, SerializationOptions options) {
		Range operationCountRange = writer.Reserve(4);
		int operationCount = 0;

		if (options.MemberSelectionMode == MemberSelectionMode.All) {
			WriteOperationType(writer, NetworkCollectionOperationType.Clear);
			operationCount++;

			for (int index = 0; index < Items.Count; index++) {
				WriteOperationType(writer, NetworkCollectionOperationType.Add);
				Range itemLengthRange = writer.Reserve(4);
				int itemStart = writer.WrittenCount;
				Codec.SerializeFull(writer, Items[index], context, options);
				writer.WriteInt32(itemLengthRange, writer.WrittenCount - itemStart);
				operationCount++;
			}
		} else {
			HashSet<int> touchedIndices = [];
			foreach (NetworkCollectionOperation<T> operation in OperationBuffer) {
				WriteOperation(writer, operation, context, options);
				operationCount++;
				if (operation.Index >= 0) {
					touchedIndices.Add(operation.Index);
				}
			}

			if (Codec.IsNetworkObject) {
				for (int index = 0; index < Items.Count; index++) {
					if (touchedIndices.Contains(index) || Items[index] is not NetworkObject item || !NetworkCollectionSerializer.HasDirtyState(item)) {
						continue;
					}

					WriteOperationType(writer, NetworkCollectionOperationType.Update);
					writer.WriteInt32(index);
					Range itemLengthRange = writer.Reserve(4);
					int itemStart = writer.WrittenCount;
					Codec.SerializeUpdate(writer, Items[index], context, options);
					writer.WriteInt32(itemLengthRange, writer.WrittenCount - itemStart);
					operationCount++;
				}
			}
		}

		writer.WriteInt32(operationCountRange, operationCount);
	}

	void INetworkCollection.Deserialize(ReadOnlySpan<byte> data, SerializationContext context) {
		NetworkCollectionReader reader = new(data);
		int operationCount = reader.ReadOperationCount();

		for (int operationIndex = 0; operationIndex < operationCount; operationIndex++) {
			NetworkCollectionOperationType operationType = reader.ReadOperationType();

			switch (operationType) {
				case NetworkCollectionOperationType.Add: {
					T item = Codec.DeserializeFull<T>(reader.ReadPayload(), context);
					AddDeserialized(item);
					break;
				}
				case NetworkCollectionOperationType.Insert: {
					int index = reader.ReadInt32();
					ValidateDeserializedIndex(index, allowEnd: true);
					T item = Codec.DeserializeFull<T>(reader.ReadPayload(), context);
					InsertDeserialized(index, item);
					break;
				}
				case NetworkCollectionOperationType.Remove: {
					int index = reader.ReadInt32();
					ValidateDeserializedIndex(index);
					RemoveAtDeserialized(index);
					break;
				}
				case NetworkCollectionOperationType.Set: {
					int index = reader.ReadInt32();
					ValidateDeserializedIndex(index);
					T item = Codec.DeserializeFull<T>(reader.ReadPayload(), context);
					SetDeserialized(index, item);
					break;
				}
				case NetworkCollectionOperationType.Clear:
					ClearDeserialized();
					break;
				case NetworkCollectionOperationType.Update: {
					int index = reader.ReadInt32();
					ValidateDeserializedIndex(index);
					ReadOnlySpan<byte> payload = reader.ReadPayload();
					T item = Items[index];
					if (item is not NetworkObject) {
						throw new InvalidOperationException("Collection update target must be a non-null NetworkObject.");
					}

					try {
						Codec.DeserializeUpdate(item, payload, context);
					} finally {
						// A nested serializer can throw after modifying the live child. Forward its
						// current dirty state, never the possibly malformed incoming bytes.
						OperationBuffer.Add(new NetworkCollectionOperation<T>(NetworkCollectionOperationType.Update, index, item));
						MarkOwnerModified();
					}
					break;
				}
				default:
					throw new InvalidOperationException($"Unsupported collection operation '{operationType}'.");
			}
		}

		reader.EnsureFullyConsumed();
	}

	void INetworkCollection.ClearDirtyState(SerializationContext context) {
		OperationBuffer.Clear();
		if (!Codec.IsNetworkObject) {
			return;
		}

		foreach (T item in Items) {
			if (item is NetworkObject networkObject &&
			    context.TypeCatalogue.TryFindSerializer(networkObject.GetType(), out INetworkObjectSerializer? serializer)) {
				serializer.ClearDirtyState(networkObject, context);
			}
		}
	}

	IEnumerator IEnumerable.GetEnumerator() {
		return GetEnumerator();
	}

	protected virtual void ValidateItemForAssignment(T item) {
	}

	protected virtual void OnItemAdded(T item) {
	}

	protected virtual void OnItemRemoving(T item) {
	}

	protected void AddDeserialized(T item) {
		ValidateItemForAssignment(item);
		Items.Add(item);
		OnItemAdded(item);
		OperationBuffer.Add(new NetworkCollectionOperation<T>(NetworkCollectionOperationType.Add, Items.Count - 1, item));
		MarkOwnerModified();
		ItemAdded?.Invoke(this, Items.Count - 1);
	}

	protected void InsertDeserialized(int index, T item) {
		ValidateItemForAssignment(item);
		Items.Insert(index, item);
		OnItemAdded(item);
		OperationBuffer.Add(new NetworkCollectionOperation<T>(NetworkCollectionOperationType.Insert, index, item));
		MarkOwnerModified();
		ItemAdded?.Invoke(this, index);
	}

	protected void SetDeserialized(int index, T item) {
		T previous = Items[index];
		ValidateItemForAssignment(item);
		OnItemRemoving(previous);
		Items[index] = item;
		OnItemAdded(item);
		OperationBuffer.Add(new NetworkCollectionOperation<T>(NetworkCollectionOperationType.Set, index, item));
		MarkOwnerModified();
		IndexChanged?.Invoke(this, index);
	}

	protected void RemoveAtDeserialized(int index) {
		T item = Items[index];
		OnItemRemoving(item);
		Items.RemoveAt(index);
		OperationBuffer.Add(new NetworkCollectionOperation<T>(NetworkCollectionOperationType.Remove, index));
		MarkOwnerModified();
		ItemRemoved?.Invoke(this, index);
	}

	protected void ClearDeserialized() {
		int removedCount = Items.Count;
		for (int index = Items.Count - 1; index >= 0; index--) {
			OnItemRemoving(Items[index]);
		}

		Items.Clear();
		OperationBuffer.Add(new NetworkCollectionOperation<T>(NetworkCollectionOperationType.Clear));
		MarkOwnerModified();
		for (int index = removedCount - 1; index >= 0; index--) {
			ItemRemoved?.Invoke(this, index);
		}
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

	private void WriteOperation(BufferWriter writer, NetworkCollectionOperation<T> operation, SerializationContext context, SerializationOptions options) {
		WriteOperationType(writer, operation.OperationType);
		switch (operation.OperationType) {
			case NetworkCollectionOperationType.Add: {
				Range itemLengthRange = writer.Reserve(4);
				int itemStart = writer.WrittenCount;
				Codec.SerializeFull(writer, operation.Value!, context, options);
				writer.WriteInt32(itemLengthRange, writer.WrittenCount - itemStart);
				break;
			}
			case NetworkCollectionOperationType.Insert: {
				writer.WriteInt32(operation.Index);
				Range itemLengthRange = writer.Reserve(4);
				int itemStart = writer.WrittenCount;
				Codec.SerializeFull(writer, operation.Value!, context, options);
				writer.WriteInt32(itemLengthRange, writer.WrittenCount - itemStart);
				break;
			}
			case NetworkCollectionOperationType.Remove:
				writer.WriteInt32(operation.Index);
				break;
			case NetworkCollectionOperationType.Set: {
				writer.WriteInt32(operation.Index);
				Range itemLengthRange = writer.Reserve(4);
				int itemStart = writer.WrittenCount;
				Codec.SerializeFull(writer, operation.Value!, context, options);
				writer.WriteInt32(itemLengthRange, writer.WrittenCount - itemStart);
				break;
			}
			case NetworkCollectionOperationType.Clear:
				break;
			case NetworkCollectionOperationType.Update: {
				writer.WriteInt32(operation.Index);
				Range itemLengthRange = writer.Reserve(4);
				int itemStart = writer.WrittenCount;
				Codec.SerializeUpdate(writer, operation.Value!, context, options);
				writer.WriteInt32(itemLengthRange, writer.WrittenCount - itemStart);
				break;
			}
			default:
				throw new InvalidOperationException($"Unsupported collection operation '{operation.OperationType}'.");
		}
	}

	private void ValidateDeserializedIndex(int index, bool allowEnd = false) {
		if (index < 0 || index > Items.Count || (!allowEnd && index == Items.Count)) {
			throw new InvalidOperationException($"Collection operation index '{index}' is out of range.");
		}
	}

	private static void WriteOperationType(BufferWriter writer, NetworkCollectionOperationType operationType) {
		writer.WriteByte((byte)operationType);
	}
}
