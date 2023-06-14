using System.Collections.Generic;

namespace Cat.Network;


public enum NetworkCollectionOperationType : byte {
	Add,
	Remove,
	Set,
	Clear
}

public struct NetworkCollectionOperation<T> {
	public NetworkCollectionOperationType OperationType { get; set; }

	public int Index { get; set; }
	public T Value { get; set; }

}

public interface INetworkCollection<T> where T : struct {
	List<NetworkCollectionOperation<T>> OperationBuffer { get; }

	void AssertOwner();
	
	void ProcessOperation(NetworkCollectionOperation<T> operation);

}
