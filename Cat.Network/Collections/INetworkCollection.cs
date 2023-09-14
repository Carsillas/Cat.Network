using System.Collections.Generic;

namespace Cat.Network.Collections;


public interface INetworkCollection<T> {
	List<NetworkCollectionOperation<T>> OperationBuffer { get; }

	void AssertOwner();
	
	void ProcessOperation(NetworkCollectionOperation<T> operation);

}
