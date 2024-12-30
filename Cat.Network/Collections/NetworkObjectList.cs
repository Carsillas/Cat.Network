using System;
using System.Collections.Generic;

namespace Cat.Network.Collections;


public interface INetworkObjectList {
	void MarkForUpdate(int index);

}

public sealed class NetworkObjectList<T> : NetworkList<T>, INetworkObjectList where T : NetworkDataObject, INetworkDataObject {
	private bool FixedSize { get; }
	
	public NetworkObjectList(NetworkEntity owner, List<T> list, bool fixedSize) : base(owner, list) {
		FixedSize = fixedSize;
	}

	protected override void AssertValidAddition(T item) {
		base.AssertValidAddition(item);
		
		if (FixedSize) {
			throw new InvalidOperationException("Attempted to modify a collection of fixed size.");
		}
		
		if (item?.Parent != null) {
			throw new InvalidOperationException($"{nameof(NetworkDataObject)}s may only occupy one networked property or list!");
		}
	}

	protected override void AssertValidRemoval() {
		base.AssertValidRemoval();
		
		if (FixedSize) {
			throw new InvalidOperationException("Attempted to modify a collection of fixed size.");
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