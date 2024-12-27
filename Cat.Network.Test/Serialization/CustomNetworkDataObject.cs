namespace Cat.Network.Test.Serialization; 

public partial record CustomNetworkDataObject : NetworkDataObject {
	
	[PropertyChangedEvent]
	int NetworkProperty.Test { get; set; }
	
	
	
}