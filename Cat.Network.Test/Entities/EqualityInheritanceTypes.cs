namespace Cat.Network.Test.Entities;

[NetworkObject]
public partial class EqualityBaseState : NetworkObject {
	[NetworkProperty]
	private partial int BaseValue { get; set; }

	[NetworkCollection]
	private partial NetworkList<int> BaseValues { get; }

	[NetworkProperty]
	public partial double Score { get; set; }

	[NetworkProperty]
	public partial int? Optional { get; set; }

	[NetworkProperty]
	public partial EqualityToken Token { get; set; }

	public int IgnoredValue { get; set; }

	// Generated equality must inspect the runtime type even when a user member hides GetType.
	public new Type GetType() => typeof(EqualityBaseState);

	public void SetBaseValue(int value) => BaseValue = value;

	public void AddBaseItem(int value) => BaseValues.Add(value);

	public void SetBaseItem(int value) => BaseValues[0] = value;
}

[NetworkObject]
public partial class EqualityDerivedState : EqualityBaseState {
	[NetworkProperty]
	public partial int DerivedValue { get; set; }

	[NetworkCollection]
	private partial NetworkDictionary<int, int> DerivedValues { get; }

	public void SetDerivedItem(int key, int value) => DerivedValues[key] = value;
}

[NetworkObject]
public sealed partial class EqualityLeafState : EqualityDerivedState {
	[NetworkProperty]
	public partial string? LeafValue { get; set; }

	[NetworkCollection]
	public partial NetworkList<int> LeafValues { get; }
}

[NetworkObject]
public sealed partial class EqualitySiblingState : EqualityBaseState {
	[NetworkProperty]
	public partial int DerivedValue { get; set; }
}

[NetworkObject]
public sealed partial class EqualityEmptyLeafState : EqualityDerivedState {
}

[NetworkObject]
public abstract partial class EqualityAbstractState : NetworkObject {
	[NetworkProperty]
	public partial int AbstractValue { get; set; }
}

[NetworkObject]
public sealed partial class EqualityConcreteState : EqualityAbstractState {
	[NetworkProperty]
	public partial int ConcreteValue { get; set; }
}

[NetworkObject]
public sealed partial class EqualityContainerState : NetworkObject {
	[NetworkProperty]
	public partial EqualityBaseState? Child { get; set; }

	[NetworkCollection]
	public partial NetworkList<EqualityBaseState?> Children { get; }

	[NetworkCollection]
	public partial NetworkDictionary<int, EqualityBaseState?> ById { get; }
}

public struct EqualityToken : IEquatable<EqualityToken> {
	public int Value;

	public bool Equals(EqualityToken other) => Value % 10 == other.Value % 10;

	public override bool Equals(object? obj) => obj is EqualityToken other && Equals(other);

	public override int GetHashCode() => Value % 10;
}
