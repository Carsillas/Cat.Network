namespace Cat.Network.Test.Entities;

[NetworkEntity]
public partial class TestNetworkEntity : NetworkEntity {

	[NetworkProperty]
	public partial int Health { get; set; }

}
