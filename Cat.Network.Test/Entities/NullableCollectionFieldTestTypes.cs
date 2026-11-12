namespace Cat.Network.Test.Entities;

public struct NullableCollectionFieldBox<T> {
	public T Field;
}

public struct NullableCollectionFieldInner {
	public int? Number;
	public Guid? Id;
}

public struct NullableCollectionFieldMiddle {
	public NullableCollectionFieldInner? First;
	public NullableCollectionFieldInner? Second;
}

public struct NullableCollectionFieldValue {
	public NullableCollectionFieldMiddle? Field;
	public NullableCollectionFieldBox<NullableCollectionFieldBox<int>>? Finite;
	public NullableCollectionFieldBox<NullableCollectionFieldBox<int>>? Sibling;
}

public struct NullableCollectionFieldKey {
	public string? Label;
}

[NetworkObject]
public partial class NullableCollectionFieldState : NetworkObject {
	[NetworkCollection]
	public partial NetworkList<NullableCollectionFieldValue> Values { get; }

	[NetworkCollection]
	public partial NetworkDictionary<int, NullableCollectionFieldValue> Lookup { get; }

	[NetworkCollection]
	public partial NetworkDictionary<NullableCollectionFieldKey, int> Labels { get; }
}
