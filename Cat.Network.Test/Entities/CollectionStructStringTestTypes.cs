namespace Cat.Network.Test.Entities;

public struct CollectionStringThenInt {
	public int Value;
	public string Text;
}

public struct CollectionStringPair {
	public string? First;
	public string? Second;
	public int Tail;
}

public struct CollectionStringEnvelope {
	public CollectionStringPair Details;
	public CollectionStringThenInt? Optional;
	public string? Suffix;
	public int Tail;
}

public struct CollectionStringOnly {
	public string? Value;
}

[NetworkObject]
public partial class CollectionStructStringState : NetworkObject {
	[NetworkCollection]
	public partial NetworkList<CollectionStringEnvelope?> Envelopes { get; }

	[NetworkCollection]
	public partial NetworkDictionary<CollectionStringPair, CollectionStringEnvelope?> Map { get; }

	[NetworkCollection]
	public partial NetworkList<CollectionStringThenInt> Mixed { get; }

	[NetworkCollection]
	public partial NetworkList<CollectionStringPair> Pairs { get; }

	[NetworkProperty]
	public partial CollectionStringEnvelope Sample { get; set; }
}
