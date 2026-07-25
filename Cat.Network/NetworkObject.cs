namespace Cat.Network;

[NetworkObject]
public partial class NetworkObject : INetworkObject {
	protected NetworkObject() {
		((INetworkObject)this).Initialize();
	}

	NetworkPropertyState[] INetworkObject.PropertyStates { get; set; } = [];

	NetworkObject? INetworkObject.Parent { get; set; }

	int INetworkObject.PropertyIndex { get; set; } = -1;

	INetworkAnchor? INetworkObject.Anchor => ((INetworkObject?)((INetworkObject)this).Parent)?.Anchor;
}
