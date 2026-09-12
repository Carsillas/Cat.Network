namespace Cat.Network.Test.Entities;

[NetworkObject]
public partial class ListIndexShiftState : NetworkObject {
	[NetworkCollection]
	public partial NetworkList<ListIndexShiftChildState?> Children { get; }
}

[NetworkObject]
public partial class ListIndexShiftChildState : NetworkObject {
	[NetworkProperty]
	public partial int Value { get; set; }

	[NetworkCollection]
	public partial NetworkList<int> Values { get; }
}
