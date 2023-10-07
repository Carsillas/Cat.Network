namespace Cat.Network.Collections;

public struct NetworkCollectionOperation<T> {
	public NetworkCollectionOperationType OperationType { get; set; }

	public int Index { get; set; }
	public T Value { get; set; }

}