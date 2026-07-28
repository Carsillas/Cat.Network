namespace Cat.Network;

public sealed class NetworkObjectList<T> : NetworkList<T> where T : NetworkObject {
	protected override void ValidateItemForAssignment(T item) {
		ArgumentNullException.ThrowIfNull(item);

		INetworkObject networkObject = item;
		if (networkObject.Parent is not null) {
			throw new InvalidOperationException("NetworkObjects may only occupy one networked property or list at a time.");
		}
	}

	protected override void OnItemAdded(T item) {
		INetworkObject networkObject = item;
		networkObject.Parent = Owner;
		networkObject.PropertyIndex = PropertyIndex;
	}

	protected override void OnItemRemoving(T item) {
		INetworkObject networkObject = item;
		networkObject.Parent = null;
		networkObject.PropertyIndex = -1;
	}
}
