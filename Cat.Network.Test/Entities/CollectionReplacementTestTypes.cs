namespace Cat.Network.Test.Entities;

[NetworkObject]
public partial class CollectionReplacementDerivedState : DirtyChildState {
	[NetworkProperty]
	public partial int Detail { get; set; }
}

public struct CollectionReplacementValue {
	public int Value;
}
