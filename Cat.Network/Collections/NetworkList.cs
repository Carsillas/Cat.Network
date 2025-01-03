using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Collections;

public abstract class NetworkList<T> : INetworkCollection<T>, IEnumerable<T> {

	public NetworkEntity Owner { get; }
	private List<T> InternalList { get; }
	public int Count => InternalList.Count;
	public bool FixedSize { get; }
	
	private ISerializationContext SerializationContext => ((INetworkEntity)Owner).SerializationContext;
	List<NetworkCollectionOperation<T>> INetworkCollection<T>.OperationBuffer { get; } = new();

	public delegate void CollectionChangedEvent(NetworkList<T> sender, int index);
	
	public event CollectionChangedEvent ItemAdded;
	public event CollectionChangedEvent ItemRemoved;
	public event CollectionChangedEvent IndexChanged;
	
	internal NetworkList(NetworkEntity owner, List<T> list, bool fixedSize) {
		Owner = owner;
		InternalList = list;
		FixedSize = fixedSize;
	}

	
	protected virtual void AssertValidAddition(T item) {
		if (FixedSize) {
			throw new InvalidOperationException("Attempted to modify a collection of fixed size.");
		}
	}

	protected virtual void AssertValidRemoval() {
		if (FixedSize) {
			throw new InvalidOperationException("Attempted to modify a collection of fixed size.");
		}
	}

	protected virtual void OnItemAdded(T item, int index) {
		
	}
	
	protected virtual void OnItemRemoved(T item) {
		
	}

	public void Add(T item) {
		Insert(InternalList.Count, item);
	}
	
	public void Insert(int index, T item) {
		((INetworkCollection<T>)this).AssertOwner();
		AssertValidAddition(item);
		
		InternalList.Insert(index, item);
		
		if (IndexChanged != null) {
			for (int i = index; i < InternalList.Count; i++) {
				IndexChanged?.Invoke(this, i);
			}
		}
		
		OnItemAdded(item, InternalList.Count - 1);
		ItemAdded?.Invoke(this, InternalList.Count - 1);
		IndexChanged?.Invoke(this, InternalList.Count - 1);

		if (SerializationContext == null) {
			return;
		}

		SerializationContext.MarkForClean(Owner);
		((INetworkCollection<T>)this).OperationBuffer.Add(new NetworkCollectionOperation<T> {
			OperationType = NetworkCollectionOperationType.Add,
			Index = InternalList.Count - 1,
			Value = item
		});
	}

	public bool Remove(T item) {
		((INetworkCollection<T>)this).AssertOwner();
		AssertValidRemoval();
		int index = InternalList.IndexOf(item);

		return RemoveAt(index);
	}
	
	public bool RemoveAt(int index) {
		((INetworkCollection<T>)this).AssertOwner();
		AssertValidRemoval();

		if (index < 0 || index >= InternalList.Count) {
			return false;
		}

		T item = InternalList[index];
		InternalList.RemoveAt(index);
		OnItemRemoved(item);
		ItemRemoved?.Invoke(this, index);

		if (IndexChanged != null) {
			for (int i = index; i < InternalList.Count; i++) {
				IndexChanged?.Invoke(this, i);
			}
		}
			
		SerializationContext.MarkForClean(Owner);
		if (SerializationContext != null) {
			((INetworkCollection<T>)this).OperationBuffer.Add(new NetworkCollectionOperation<T> {
				OperationType = NetworkCollectionOperationType.Remove,
				Index = index
			});
		}
		return true;

	}
	
	public void Clear() {
		((INetworkCollection<T>)this).AssertOwner();
		AssertValidRemoval();
		InternalList.Clear();
		
		if (SerializationContext == null) {
			return;
		}

		SerializationContext.MarkForClean(Owner);
		((INetworkCollection<T>)this).OperationBuffer.Add(new NetworkCollectionOperation<T> {
			OperationType = NetworkCollectionOperationType.Clear
		});
	}

	public bool Contains(T item) {
		return InternalList.Contains(item);
	}

	public int IndexOf(T item) { 
		return InternalList.IndexOf(item);
	}

	public T this[int index] {
		get => InternalList[index];
		set {
			((INetworkCollection<T>)this).AssertOwner();
			T previousValue = InternalList[index];
			InternalList[index] = value;
			
			OnItemRemoved(previousValue);
			OnItemAdded(value, index);
			IndexChanged?.Invoke(this, index);

			if (SerializationContext == null) {
				return;
			}

			SerializationContext.MarkForClean(Owner);
			((INetworkCollection<T>)this).OperationBuffer.Add(new NetworkCollectionOperation<T> {
				OperationType = NetworkCollectionOperationType.Set,
				Index = index,
				Value = value
			});
		}
	}
	
	public void Swap(int indexA, int indexB) {
		((INetworkCollection<T>)this).AssertOwner();
		
		if (indexA < 0 || indexA >= InternalList.Count) {
			throw new IndexOutOfRangeException($"{nameof(indexA)} out of range: {indexA}");
		}
		if (indexB < 0 || indexB >= InternalList.Count) {
			throw new IndexOutOfRangeException($"{nameof(indexB)} out of range: {indexB}");
		}

		(InternalList[indexA], InternalList[indexB]) = (InternalList[indexB], InternalList[indexA]);

		IndexChanged?.Invoke(this, indexA);
		IndexChanged?.Invoke(this, indexB);

		if (SerializationContext == null) {
			return;
		}

		SerializationContext.MarkForClean(Owner);
		((INetworkCollection<T>)this).OperationBuffer.Add(new NetworkCollectionOperation<T> {
			OperationType = NetworkCollectionOperationType.Swap,
			Index = indexA,
			SwapIndex = indexB
		});
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