using System;
using System.Collections.Generic;

namespace Cat.Network.Test.Serialization;

public partial class ReferenceTypeTestEntity : NetworkEntity {
		
	NetworkDataObject NetworkProperty.Test { get; set; }
	CustomNetworkDataObject NetworkProperty.TestDerived { get; set; }
	NestedNetworkDataObject NetworkProperty.TestNested { get; set; }
	
	List<CustomNetworkDataObject> NetworkCollection.Inventory { get; } = new();

	public event Action<CustomNetworkDataObject> ReceivedRPC;
	
	void RPC.ReferenceRPC(CustomNetworkDataObject ndo) {
		ReceivedRPC?.Invoke(ndo);
	}

}