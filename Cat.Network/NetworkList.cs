using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network;

public class NetworkList<T> : INetworkCollection<T>, IEnumerable<T> where T : struct {

	private NetworkEntity Owner { get; }
	private List<T> InternalList { get; }
	private ISerializationContext SerializationContext => ((INetworkEntity)Owner).SerializationContext;

	List<NetworkCollectionOperation<T>> INetworkCollection<T>.OperationBuffer { get; } = new();

	public NetworkList(NetworkEntity owner, List<T> list) {
		Owner = owner;
		InternalList = list;
	}

	public int Count => InternalList.Count;

	public void Add(T item) {
		((INetworkCollection<T>)this).AssertOwner();
		InternalList.Add(item);

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
		
		if (index >= 0) {
			InternalList.RemoveAt(index);
			SerializationContext.MarkForClean(Owner);
			if (SerializationContext != null) {
				((INetworkCollection<T>)this).OperationBuffer.Add(new NetworkCollectionOperation<T> {
					OperationType = NetworkCollectionOperationType.Remove,
					Index = index,
					Value = item
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
			InternalList[index] = value;

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