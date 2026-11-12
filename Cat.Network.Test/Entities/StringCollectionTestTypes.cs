namespace Cat.Network.Test.Entities;

[NetworkObject]
public partial class NullableStringCollectionState : NetworkObject {
	[NetworkCollection]
	public partial NetworkList<string?> Items { get; }

	[NetworkCollection]
	public partial NetworkDictionary<string, string?> Values { get; }
}

[NetworkObject]
public partial class StringCollectionState : NetworkObject {
	[NetworkCollection]
	public partial NetworkList<string> Items { get; }

	[NetworkCollection]
	public partial NetworkDictionary<string, string> Values { get; }
}
