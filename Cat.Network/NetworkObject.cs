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

	/// <summary>Checks ownership and parent cycles before changing an object attachment.</summary>
	/// <remarks>
	/// Used by generated setters in consumer assemblies and by object collections.
	/// Properties may retain the same owner and property index; collection items must be unattached.
	/// </remarks>
	protected internal static void ValidateAttachment(NetworkObject? value, NetworkObject? owner, int propertyIndex, bool isCollectionItem) {
		if (value is INetworkObject networkValue && networkValue.Parent is not null &&
		    (isCollectionItem || !ReferenceEquals(networkValue.Parent, owner) || networkValue.PropertyIndex != propertyIndex)) {
			throw new InvalidOperationException("NetworkObjects may only occupy one networked property or collection item at a time.");
		}

		NetworkObject? ancestor = owner;
		NetworkObject? fast = owner;
		while (ancestor is not null) {
			if (ReferenceEquals(ancestor, value)) {
				throw new InvalidOperationException("A NetworkObject cannot be attached to itself or one of its descendants.");
			}

			ancestor = ((INetworkObject)ancestor).Parent;
			// Detect an already corrupted parent chain without allocating for each assignment.
			fast = fast is null ? null : ((INetworkObject)fast).Parent;
			fast = fast is null ? null : ((INetworkObject)fast).Parent;
			if (ancestor is not null && ReferenceEquals(ancestor, fast)) {
				throw new InvalidOperationException("Cannot attach a NetworkObject to an owner with a cyclic parent chain.");
			}
		}
	}

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
