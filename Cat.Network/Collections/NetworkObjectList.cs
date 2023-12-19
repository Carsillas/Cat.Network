using System;
using System.Collections.Generic;

namespace Cat.Network.Collections;


public interface INetworkObjectList {
	void MarkForUpdate(int index);

}

public sealed class NetworkObjectList<T> : NetworkList<T>, INetworkObjectList where T : NetworkDataObject, INetworkDataObject {
	public NetworkObjectList(NetworkEntity owner, List<T> list) : base(owner, list) { }

	protected override void AssertValidAddition(T item) {
		if (item?.Parent != null) {
			throw new InvalidOperationException($"{nameof(NetworkDataObject)}s may only occupy one networked property or list!");
		}
	}

	protected override void OnItemAdded(T item) {
		if (item == null) {
			return;
		}

		item.Parent = Owner;
		item.Collection = this;
	}

	protected override void OnItemRemoved(T item) {
		if (item == null) {
			return;
		}
		
		item.Parent = null;
		item.Collection = null;
	}

	protected override void OnItemReplaced(int index, T previousItem, T newItem) {
		OnItemRemoved(previousItem);
		OnItemAdded(newItem);
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