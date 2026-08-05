namespace Cat.Network;

public abstract partial class NetworkObject : INetworkObject {
	
	// IF YOU ARE EVER THINKING ABOUT ADDING A PROPERTY TO THIS, THIS MUST ONCE AGAIN BE MARKED WITH [NetworkObject]
	protected static System.Collections.Immutable.ImmutableArray<NetworkPropertyInfo> Properties { get; } = [];

	protected NetworkObject() {
		((INetworkObject)this).Initialize();
	}

	NetworkPropertyState[] INetworkObject.PropertyStates { get; set; } = [];

	NetworkObject? INetworkObject.Parent { get; set; }

	int INetworkObject.PropertyIndex { get; set; } = -1;

	INetworkAnchor? INetworkObject.Anchor => ((INetworkObject?)((INetworkObject)this).Parent)?.Anchor;

	public abstract NetworkObject Clone();

	void INetworkObject.Initialize() {
		((INetworkObject)this).PropertyStates = new NetworkPropertyState[Properties.Length];
	}
}
