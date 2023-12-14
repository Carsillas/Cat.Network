using System;

namespace Cat.Network.Test.Server;

public partial class TestEntity : NetworkEntity {

	int NetworkProperty.Health { get; set; }

	void RPC.ModifyHealth(int amount) {
		Health += amount;
	}

	
	[AutoEvent]
	void RPC.VerifyAutoParametersRpc([Client] CatClient client, [Instigator] Guid instigatorId) {
		
	}

}