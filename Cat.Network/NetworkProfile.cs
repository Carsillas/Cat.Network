namespace Cat.Network;

[NetworkObject]
public abstract partial class NetworkProfile : NetworkObject, INetworkObject {

	public Guid Id { get; internal set; }

	private protected override NetworkObject? GetAnchor() {
		return this;
	}

}
