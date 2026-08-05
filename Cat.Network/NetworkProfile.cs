namespace Cat.Network;

[NetworkObject]
public abstract partial class NetworkProfile : NetworkObject, INetworkObject, INetworkAnchor {

	INetworkAnchor INetworkObject.Anchor => this;
	public Guid Id { get; internal set; }

}
