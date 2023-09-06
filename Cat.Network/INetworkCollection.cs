using System.Collections.Generic;

namespace Cat.Network;


public interface INetworkCollection<T> where T : struct {
	List<NetworkCollectionOperation<T>> OperationBuffer { get; }

	void AssertOwner();
	
	void ProcessOperation(NetworkCollectionOperation<T> operation);

}
