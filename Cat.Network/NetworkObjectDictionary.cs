namespace Cat.Network;

public sealed class NetworkObjectDictionary<TKey, TValue> : NetworkDictionary<TKey, TValue> where TKey : notnull where TValue : NetworkObject? {
	protected override void ValidateValueForAssignment(TValue value) {
		NetworkObject.ValidateAttachment(value, Owner, PropertyIndex, isCollectionItem: true);
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
