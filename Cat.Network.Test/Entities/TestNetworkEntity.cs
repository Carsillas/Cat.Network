namespace Cat.Network.Test.Entities;

[NetworkObject]
public partial class TestNetworkEntity : NetworkEntity {

	[NetworkProperty]
	public partial int Health { get; private set; }

}
