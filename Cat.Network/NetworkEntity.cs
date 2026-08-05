namespace Cat.Network;


[NetworkObject]
public abstract partial class NetworkEntity : NetworkObject, INetworkObject, INetworkAnchor {

	INetworkAnchor INetworkObject.Anchor => this;
	public Guid Id { get; internal set; }

	public RelayPeer? Peer { get; internal set; }

	public bool IsOwner => Peer?.Owns(this) ?? false;
	
}
