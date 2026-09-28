namespace Cat.Network.Test.Entities;

public struct FallbackEmpty { }

public struct FallbackStateless {
	public static int Shared;
	public int Computed => 42;
}

public struct FallbackHidden {
	private int value;
	public int Value => value;

	public FallbackHidden(int value) => this.value = value;
}

public struct FallbackAutoProperty {
	public int Value { get; set; }
}

public struct FallbackMixed {
	public int Value;
	private int local;
	public int Local => local;

	public FallbackMixed(int value, int local) {
		Value = value;
		this.local = local;
	}
}

public struct FallbackNested<T> {
	public T Value;
}

public struct FallbackValue {
	public int Number;
	public int? Optional;
	public FallbackEmpty? Empty;
	public FallbackNested<int>? Nested;
	public Guid Id;
}

[NetworkObject]
public partial class FallbackState : NetworkObject {
	[NetworkProperty] public partial FallbackValue Value { get; set; }
	[NetworkProperty] public partial FallbackEmpty? OptionalEmpty { get; set; }
	[NetworkCollection] public partial NetworkList<FallbackValue> Values { get; }
	[NetworkCollection] public partial NetworkList<FallbackEmpty> EmptyValues { get; }
	[NetworkCollection] public partial NetworkDictionary<FallbackEmpty, int> EmptyKeys { get; }
}
