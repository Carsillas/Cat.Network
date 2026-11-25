namespace Cat.Network.Test.Entities;

[NetworkObject]
public partial class BatchedCollectionEntityState : NetworkEntity {
	[NetworkCollection]
	public partial NetworkList<ValueCollectionState> ListChildren { get; }

	[NetworkCollection]
	public partial NetworkDictionary<int, ValueCollectionState> DictionaryChildren { get; }
}
