using System;

namespace Cat.Network.Test.Server;

public partial class TestEntity : NetworkEntity {

	int NetworkProperty.Health { get; set; }

	public int NonNetworkedVariable { get; set; }
	
	void RPC.ModifyHealth(int amount) {
		Health += amount;
	}

	[Broadcast]
	[AutoEvent]
	void RPC.SetNonNetworkedVariable(int value) {
		NonNetworkedVariable = value;
	}

	
	[AutoEvent]
	void RPC.VerifyAutoParametersRpc([Client] CatClient client, [Instigator] Guid instigatorId) {
		
	}
	
	

}