namespace Cat.Network.Test.Entities;

[NetworkObject]
public partial class CollectionCallbackListState : NetworkObject {
	[NetworkCollection]
	public partial NetworkList<ValueCollectionState> Children { get; }
}

[NetworkObject]
public partial class CollectionCallbackDictionaryState : NetworkObject {
	[NetworkCollection]
	public partial NetworkDictionary<int, ValueCollectionState> Children { get; }
}
