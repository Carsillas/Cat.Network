using System.Collections;

namespace Cat.Network;

public abstract class NetworkList<T> : IList<T> {
	protected NetworkList(NetworkObject owner, int propertyIndex) {
		Owner = owner;
		PropertyIndex = propertyIndex;
	}

	protected NetworkObject Owner { get; }

	protected int PropertyIndex { get; }

	protected List<T> Items { get; } = [];

	public int Count => Items.Count;

	public bool IsReadOnly => false;

	public T this[int index] {
		get => Items[index];
		set {
			T previous = Items[index];
			if (EqualityComparer<T>.Default.Equals(previous, value)) {
				return;
			}

			ValidateItemForAssignment(value);
			OnItemRemoving(previous);
			Items[index] = value;
			OnItemAdded(value);
			MarkOwnerModified();
		}
	}

	public void Add(T item) {
		ValidateItemForAssignment(item);
		Items.Add(item);
		OnItemAdded(item);
		MarkOwnerModified();
	}

	public void Clear() {
		if (Items.Count == 0) {
			return;
		}

		for (int index = Items.Count - 1; index >= 0; index--) {
			OnItemRemoving(Items[index]);
		}

		Items.Clear();
		MarkOwnerModified();
	}

	public bool Contains(T item) {
		return Items.Contains(item);
	}

	public void CopyTo(T[] array, int arrayIndex) {
		Items.CopyTo(array, arrayIndex);
	}

	public IEnumerator<T> GetEnumerator() {
		return Items.GetEnumerator();
	}

	public int IndexOf(T item) {
		return Items.IndexOf(item);
	}

	public void Insert(int index, T item) {
		ValidateItemForAssignment(item);
		Items.Insert(index, item);
		OnItemAdded(item);
		MarkOwnerModified();
	}

	public bool Remove(T item) {
		int index = Items.IndexOf(item);
		if (index < 0) {
			return false;
		}

		RemoveAt(index);
		return true;
	}

	public void RemoveAt(int index) {
		T item = Items[index];
		OnItemRemoving(item);
		Items.RemoveAt(index);
		MarkOwnerModified();
	}

	IEnumerator IEnumerable.GetEnumerator() {
		return GetEnumerator();
	}

	protected virtual void ValidateItemForAssignment(T item) {
	}

	protected virtual void OnItemAdded(T item) {
	}

	protected virtual void OnItemRemoving(T item) {
	}

	protected void MarkOwnerModified() {
		INetworkObject current = Owner;
		current.PropertyStates[PropertyIndex] |= NetworkPropertyState.Modified;

		while (current.Parent is NetworkObject parent) {
			INetworkObject parentObject = parent;
			parentObject.PropertyStates[current.PropertyIndex] |= NetworkPropertyState.Modified;
			current = parentObject;
		}
	}
}
