using System;
using System.Collections.Generic;

namespace Cat.Network.Collections;


public interface INetworkObjectList {
	void MarkForUpdate(int index);

}

public sealed class NetworkObjectList<T> : NetworkList<T>, INetworkObjectList where T : NetworkDataObject, INetworkDataObject {
	public NetworkObjectList(NetworkEntity owner, List<T> list) : base(owner, list) { }

	protected override void AssertValidAddition(T item) {
		if (item.Parent != null) {
			throw new InvalidOperationException($"{nameof(NetworkDataObject)}s may only occupy one networked property or list!");
		}
	}

	protected override void OnItemAdded(T item) {
		item.Parent = Owner;
		item.Collection = this;
	}

	protected override void OnItemRemoved(T item) {
		item.Parent = null;
		item.Collection = null;
	}

	public void MarkForUpdate(int index) {
		INetworkCollection<T> iNetworkCollection = this;
		
		iNetworkCollection.OperationBuffer.Add(new NetworkCollectionOperation<T> {
			Index = index,
			OperationType = NetworkCollectionOperationType.Update,
			Value = this[index]
		});
	}
}