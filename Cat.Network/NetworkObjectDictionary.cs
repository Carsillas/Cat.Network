namespace Cat.Network;

public sealed class NetworkObjectDictionary<TKey, TValue> : NetworkDictionary<TKey, TValue> where TKey : notnull where TValue : NetworkObject? {
	protected override void ValidateValueForAssignment(TValue value) {
		if (value is null) {
			return;
		}

		INetworkObject networkObject = value;
		if (networkObject.Parent is not null) {
			throw new InvalidOperationException("NetworkObjects may only occupy one networked property or list at a time.");
		}
	}

	protected override void OnValueAdded(TValue value) {
		if (value is null) {
			return;
		}

		INetworkObject networkObject = value;
		networkObject.Parent = Owner;
		networkObject.PropertyIndex = PropertyIndex;
		networkObject.IsCollectionItem = true;
	}

	protected override void OnValueRemoving(TValue value) {
		if (value is null) {
			return;
		}

		INetworkObject networkObject = value;
		networkObject.Parent = null;
		networkObject.PropertyIndex = -1;
		networkObject.IsCollectionItem = false;
	}
}
