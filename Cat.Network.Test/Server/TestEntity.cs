namespace Cat.Network.Test.Server;

public partial class TestEntity : NetworkEntity {

	int NetworkProperty.Health { get; set; }

	void RPC.ModifyHealth(int amount) {
		Health += amount;
	}

}