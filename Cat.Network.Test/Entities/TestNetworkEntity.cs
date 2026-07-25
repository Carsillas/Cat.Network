namespace Cat.Network.Test.Entities;

[NetworkObject]
public partial class TestNetworkEntity : NetworkEntity {

	[NetworkProperty]
	public partial int Health { get; private set; }
	
	[NetworkProperty]
	public partial string Test { get; private set; }

}

[NetworkObject]
public partial class TestEntity2 : TestNetworkEntity {
	
	
	[NetworkProperty]
	public partial int Health2 { get; private set; }
	
	
}
