using System;

namespace Cat.Network.Test.Server;

public partial class TestEntity : NetworkEntity {

	int NetworkProperty.Health { get; set; }

	void RPC.ModifyHealth(int amount) {
		Health += amount;
	}

	public event Action<CatClient, Guid> VerifyAutoParameters;
	
	void RPC.VerifyAutoParametersRpc([Client] CatClient client, [Instigator] Guid instigatorId) {
		VerifyAutoParameters?.Invoke(client, instigatorId);
	}

}