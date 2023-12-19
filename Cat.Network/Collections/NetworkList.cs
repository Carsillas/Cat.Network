using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Collections;

public abstract class NetworkList<T> : INetworkCollection<T>, IEnumerable<T> {

	internal NetworkEntity Owner { get; }
	private List<T> InternalList { get; }
	public int Count => InternalList.Count;
	
	private ISerializationContext SerializationContext => ((INetworkEntity)Owner).SerializationContext;
	List<NetworkCollectionOperation<T>> INetworkCollection<T>.OperationBuffer { get; } = new();

	public delegate void CollectionChangedEvent(NetworkList<T> sender, int index, T item);
	
	public event CollectionChangedEvent ItemAdded;
	public event CollectionChangedEvent ItemRemoved;
	
	
	internal NetworkList(NetworkEntity owner, List<T> list) {
		Owner = owner;
		InternalList = list;
	}



	protected virtual void AssertValidAddition(T item) {
		
	}
	
	protected virtual void OnItemAdded(T item) {
		
	}
	
	protected virtual void OnItemRemoved(T item) {
		
	}

	public void Add(T item) {
		((INetworkCollection<T>)this).AssertOwner();
		AssertValidAddition(item);
		
		InternalList.Add(item);
		OnItemAdded(item);
		ItemAdded?.Invoke(this, InternalList.Count - 1, item);

		if(SerializationContext != null) {
			SerializationContext.MarkForClean(Owner);
			((INetworkCollection<T>)this).OperationBuffer.Add(new NetworkCollectionOperation<T> {
				OperationType = NetworkCollectionOperationType.Add,
				Index = InternalList.Count - 1,
				Value = item
			});
		}
	}

	public bool Remove(T item) {
		((INetworkCollection<T>)this).AssertOwner();
		int index = InternalList.IndexOf(item);

		return RemoveAt(index);
	}
	
	public bool RemoveAt(int index) {
		((INetworkCollection<T>)this).AssertOwner();
		
		if (index >= 0 && index < InternalList.Count) {
			T item = InternalList[index];
			InternalList.RemoveAt(index);
			OnItemRemoved(item);
			ItemRemoved?.Invoke(this, index, item);
			SerializationContext.MarkForClean(Owner);
			if (SerializationContext != null) {
				((INetworkCollection<T>)this).OperationBuffer.Add(new NetworkCollectionOperation<T> {
					OperationType = NetworkCollectionOperationType.Remove,
					Index = index
				});
			}
			return true;
		}

		return false;
	}
	
	public void Clear() {
		((INetworkCollection<T>)this).AssertOwner();
		InternalList.Clear();
		if (SerializationContext != null) {
			SerializationContext.MarkForClean(Owner);
			((INetworkCollection<T>)this).OperationBuffer.Add(new NetworkCollectionOperation<T> {
				OperationType = NetworkCollectionOperationType.Clear
			});
		}
	}

	public bool Contains(T item) {
		return InternalList.Contains(item);
	}

	public int IndexOf(T item) { 
		return InternalList.IndexOf(item);
	}

	public T this[int index] {
		get {
			return InternalList[index];
		}
		set {
			((INetworkCollection<T>)this).AssertOwner();
			T previousValue = InternalList[index];
			InternalList[index] = value;
			
			OnItemRemoved(previousValue);
			ItemRemoved?.Invoke(this, index, previousValue);
			OnItemAdded(value);
			ItemAdded?.Invoke(this, index, value);

			if (SerializationContext != null) {
				SerializationContext.MarkForClean(Owner);
				((INetworkCollection<T>)this).OperationBuffer.Add(new NetworkCollectionOperation<T> {
					OperationType = NetworkCollectionOperationType.Set,
					Index = index,
					Value = value
				});
			}
		}
	}

	public List<T>.Enumerator GetEnumerator() {
		return InternalList.GetEnumerator();
	}

	void INetworkCollection<T>.AssertOwner() {
		if (!Owner.IsOwner) {
			throw new InvalidOperationException("Cannot modify a network collection of which you are not the owner!");
		}
	}

	void INetworkCollection<T>.ProcessOperation(NetworkCollectionOperation<T> operation) {



	}

	IEnumerator<T> IEnumerable<T>.GetEnumerator() {
		return InternalList.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator() {
		return InternalList.GetEnumerator();
	}
}