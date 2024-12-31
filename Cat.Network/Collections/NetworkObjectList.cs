using System;
using System.Collections.Generic;

namespace Cat.Network.Collections;


public interface INetworkObjectList {
	void MarkForUpdate(int index);

}

public sealed class NetworkObjectList<T> : NetworkList<T>, INetworkObjectList where T : NetworkDataObject, INetworkDataObject {
	
	public NetworkObjectList(NetworkEntity owner, List<T> list, bool fixedSize) : base(owner, list, fixedSize) { }

	protected override void AssertValidAddition(T item) {
		base.AssertValidAddition(item);
		
		if (item?.Parent != null) {
			throw new InvalidOperationException($"{nameof(NetworkDataObject)}s may only occupy one networked property or list!");
		}
	}

	protected override void OnItemAdded(T item, int index) {
		if (item == null) {
			return;
		}

		item.Parent = Owner;
		item.Collection = this;
		item.PropertyIndex = index;
	}

	protected override void OnItemRemoved(T item) {
		if (item == null) {
			return;
		}
		
		item.Parent = null;
		item.Collection = null;
		item.PropertyIndex = -1;
	}
	
	public bool RemoveByReference(T item) {
		((INetworkCollection<T>)this).AssertOwner();
		AssertValidRemoval();
		int index = IndexOfByReference(item);
		return RemoveAt(index);
	}
	
	public int IndexOfByReference(T item) {
		for (int i = 0; i < Count; i++) {
			if (ReferenceEquals(this[i], item)) {
				return i;
			}
		}

		return -1;
	}

	public bool ContainsByReference(T item) {
		for (int i = 0; i < Count; i++) {
			if (ReferenceEquals(this[i], item)) {
				return true;
			}
		}
		return false;
	}
	
	public void MarkForUpdate(int index) {
		INetworkCollection<T> iNetworkCollection = this;

		if (iNetworkCollection.OperationBuffer.Count > 0) {
			NetworkCollectionOperation<T> lastOperation = iNetworkCollection.OperationBuffer[^1];

			if (lastOperation.OperationType == NetworkCollectionOperationType.Update &&
			    lastOperation.Index == index) {
				return;
			}
		}
		
		iNetworkCollection.OperationBuffer.Add(new NetworkCollectionOperation<T> {
			Index = index,
			OperationType = NetworkCollectionOperationType.Update,
			Value = this[index]
		});
	}
}