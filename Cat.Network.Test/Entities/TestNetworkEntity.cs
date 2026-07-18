namespace Cat.Network.Test.Entities;

[NetworkEntity]
public sealed partial class TestNetworkEntity : NetworkEntity {
	
	[NetworkProperty]
	public partial int Health { get; set; }
	
}
