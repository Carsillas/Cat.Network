using System.Collections.Generic;

namespace Cat.Network.Test.Serialization;

public partial class ReferenceTypeTestEntity : NetworkEntity {
		
	NetworkDataObject NetworkProperty.Test { get; set; }
	CustomNetworkDataObject NetworkProperty.TestDerived { get; set; }
	
	List<CustomNetworkDataObject> NetworkCollection.Inventory { get; } = new();

}