namespace Cat.Network.Test.Entities;

[NetworkObject]
public partial class PrimitiveState : NetworkObject {
	[NetworkProperty]
	public partial int Health { get; private set; }

	[NetworkProperty]
	public partial string Name { get; private set; } = string.Empty;
}

[NetworkObject]
public partial class NullableState : NetworkObject {
	[NetworkProperty]
	public partial int? Health { get; private set; }

	[NetworkProperty]
	public partial Guid? SessionId { get; private set; }
}

[NetworkObject]
public partial class ActorState : NetworkObject {
	[NetworkProperty]
	public partial int Health { get; private set; }
}

[NetworkObject]
public partial class PlayerState : ActorState {
	[NetworkProperty]
	public partial int Mana { get; private set; }
}

public struct UltraDetailedStats {
	public double Vision;
}

public struct DetailStats {
	public double CriticalChance;
	public UltraDetailedStats? UltraDetails;
}

public struct Stats {
	public float Accuracy;
	public DetailStats Details;
	public int Health;
	public string Name;
	public Guid? SessionId;
}

[NetworkObject]
public partial class StructState : NetworkObject {
	[NetworkProperty]
	public partial Stats? PreviousStats { get; set; }

	[NetworkProperty]
	public partial Stats Stats { get; set; }
}

[NetworkObject]
public partial class ChildState : NetworkObject {
	[NetworkProperty]
	public partial int Value { get; private set; }
}

[NetworkObject]
public partial class ReplacementChildState : ChildState {
	[NetworkProperty]
	public partial int Bonus { get; private set; }
}

[NetworkObject]
public partial class ParentState : NetworkObject {
	[NetworkProperty]
	public partial ChildState? Child { get; private set; }
}
