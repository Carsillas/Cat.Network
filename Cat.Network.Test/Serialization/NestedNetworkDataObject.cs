namespace Cat.Network.Test.Serialization; 

public partial record NestedNetworkDataObject : NetworkDataObject {
	
	CustomNetworkDataObject NetworkProperty.NestedProperty { get; set; }
	
}