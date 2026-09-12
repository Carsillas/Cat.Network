namespace Cat.Network.Test.Entities;

[NetworkObject]
public partial class CloneDefaultChildrenState : NetworkObject {
	public CloneDefaultChildrenState() {
		Constructed = new();
		InitializedDefault = Initialized!;
		ConstructedDefault = Constructed;
		NonNullableDefault = NonNullable;
	}

	[NetworkProperty]
	public partial ChildState? Initialized { get; set; } = new();

	[NetworkProperty]
	public partial ChildState? Constructed { get; set; }

	[NetworkProperty]
	public partial ChildState NonNullable { get; set; } = new();

	public ChildState InitializedDefault { get; }
	public ChildState ConstructedDefault { get; }
	public ChildState NonNullableDefault { get; }
}

[NetworkObject]
public partial class CloneDerivedDefaultChildrenState : CloneDefaultChildrenState {
	[NetworkProperty]
	public partial int Level { get; set; }
}

[NetworkObject]
public partial class CloneDefaultChildContainer : NetworkObject {
	public CloneDefaultChildContainer() {
		Child = new();
		ConstructedDefault = Child;
	}

	[NetworkProperty]
	public partial CloneDefaultChildrenState? Child { get; set; }

	public CloneDefaultChildrenState ConstructedDefault { get; }
}
