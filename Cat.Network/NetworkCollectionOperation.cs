namespace Cat.Network;

public readonly struct NetworkCollectionOperation<T> {
	public NetworkCollectionOperation(NetworkCollectionOperationType operationType, int index = -1, T? value = default) {
		OperationType = operationType;
		Index = index;
		Value = value;
	}

	public NetworkCollectionOperationType OperationType { get; }

	public int Index { get; }

	public T? Value { get; }
}
