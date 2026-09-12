namespace Cat.Network;

public sealed class NetworkObjectList<T> : NetworkList<T> where T : NetworkObject? {
	protected override void ValidateItemForAssignment(T item) {
		NetworkObject.ValidateAttachment(item, Owner, PropertyIndex, isCollectionItem: true);
	}

	protected override void OnItemAdded(T item) {
		if (item is null) {
			return;
		}

		INetworkObject networkObject = item;
		networkObject.Parent = Owner;
		networkObject.PropertyIndex = PropertyIndex;
		networkObject.IsCollectionItem = true;
	}

	protected override void OnItemRemoving(T item) {
		if (item is null) {
			return;
		}

		INetworkObject networkObject = item;
		networkObject.Parent = null;
		networkObject.PropertyIndex = -1;
		networkObject.IsCollectionItem = false;
	}
}
