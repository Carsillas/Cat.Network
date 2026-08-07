namespace Cat.Network;

public interface INetworkObject {
	
	int PropertyIndex { get; set; }
	INetworkAnchor? Anchor { get; }
	bool IsCollectionItem { get; set; }
	System.Collections.Immutable.ImmutableArray<NetworkPropertyInfo> NetworkProperties { get; }
	NetworkObject? Parent { get; set; }
	NetworkPropertyState[] PropertyStates { get; set; }
	void OnPropertyChanged(PropertyChangedEventArgs args);
	
	void Initialize();
}
