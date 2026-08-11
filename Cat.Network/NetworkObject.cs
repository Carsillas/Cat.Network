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

	bool INetworkObject.IsCollectionItem { get; set; }

	System.Collections.Immutable.ImmutableArray<NetworkPropertyInfo> INetworkObject.NetworkProperties => Properties;

	public NetworkObject? Anchor => GetAnchor();

	public bool IsOwner => GetIsOwner();

	public event NetworkPropertyChanged? PropertyChanged;

	public abstract NetworkObject Clone();

	private protected virtual NetworkObject? GetAnchor() {
		return ((INetworkObject?)((INetworkObject)this).Parent)?.Anchor;
	}

	private protected virtual bool GetIsOwner() {
		NetworkObject? anchor = Anchor;
		return !ReferenceEquals(anchor, this) && (anchor?.IsOwner ?? false);
	}

	void INetworkObject.OnPropertyChanged(PropertyChangedEventArgs args) {
		PropertyChanged?.Invoke(this, args);

		INetworkObject networkObject = this;
		INetworkObject? parentNetworkObject = networkObject.Parent;
		if (networkObject.IsCollectionItem || parentNetworkObject is null || networkObject.PropertyIndex < 0) {
			return;
		}

		int propertyIndex = networkObject.PropertyIndex;
		System.Collections.Immutable.ImmutableArray<NetworkPropertyInfo> parentProperties = parentNetworkObject.NetworkProperties;
		string propertyName = propertyIndex < parentProperties.Length
			? parentProperties[propertyIndex].Name
			: string.Empty;

		parentNetworkObject.OnPropertyChanged(new PropertyChangedEventArgs {
			Index = propertyIndex,
			Name = propertyName
		});
	}

	void INetworkObject.Initialize() {
		((INetworkObject)this).PropertyStates = new NetworkPropertyState[Properties.Length];
	}
}
