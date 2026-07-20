namespace Cat.Network;


[NetworkObject]
public abstract partial class NetworkEntity : NetworkObject {

	public Guid Id { get; internal set; }

	
}
