using System;
using System.Collections.Generic;
using Cat.Network.Collections;

namespace Cat.Network.Test.Serialization;

public partial class ReferenceTypeTestEntity : NetworkEntity {
		
	NetworkDataObject NetworkProperty.Test { get; set; }
	CustomNetworkDataObject NetworkProperty.TestDerived { get; set; }
	NestedNetworkDataObject NetworkProperty.TestNested { get; set; }
	
	List<CustomNetworkDataObject> NetworkCollection.Inventory { get; } = new();
	
	[FixedSize]
	List<CustomNetworkDataObject> NetworkCollection.TestPreInitializedCollection { get; } = [
		new CustomNetworkDataObject {
			Test = 123
		}
	];
	

	public event Action<CustomNetworkDataObject> ReceivedRpc;
	
	void RPC.ReferenceRpc(CustomNetworkDataObject ndo) {
		ReceivedRpc?.Invoke(ndo);
	}

}