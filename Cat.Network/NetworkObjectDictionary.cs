namespace Cat.Network;

public sealed class NetworkObjectDictionary<TKey, TValue> : NetworkDictionary<TKey, TValue> where TKey : notnull where TValue : NetworkObject {
	protected override void ValidateValueForAssignment(TValue value) {
		ArgumentNullException.ThrowIfNull(value);

		INetworkObject networkObject = value;
		if (networkObject.Parent is not null) {
			throw new InvalidOperationException("NetworkObjects may only occupy one networked property or list at a time.");
		}
	}

	protected override void OnValueAdded(TValue value) {
		INetworkObject networkObject = value;
		networkObject.Parent = Owner;
		networkObject.PropertyIndex = PropertyIndex;
	}

	protected override void OnValueRemoving(TValue value) {
		INetworkObject networkObject = value;
		networkObject.Parent = null;
		networkObject.PropertyIndex = -1;
	}
}
