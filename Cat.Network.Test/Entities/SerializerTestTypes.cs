namespace Cat.Network.Test.Entities;

[NetworkObject]
public partial class PrimitiveState : NetworkObject {
	public static PrimitiveState Create(int health, string name) {
		return new PrimitiveState {
			Health = health,
			Name = name
		};
	}

	[NetworkProperty]
	public partial int Health { get; private set; }

	[NetworkProperty]
	public partial string Name { get; private set; } = string.Empty;
}

[NetworkObject]
public partial class NullableState : NetworkObject {
	public static NullableState Create(int? health, Guid? sessionId) {
		return new NullableState {
			Health = health,
			SessionId = sessionId
		};
	}

	[NetworkProperty]
	public partial int? Health { get; private set; }

	[NetworkProperty]
	public partial Guid? SessionId { get; private set; }
}

[NetworkObject]
public partial class ActorState : NetworkObject {
	protected static void InitializeHealth(ActorState target, int health) {
		target.Health = health;
	}

	[NetworkProperty]
	public partial int Health { get; private set; }
}

[NetworkObject]
public partial class PlayerState : ActorState {
	public static PlayerState Create(int health, int mana) {
		PlayerState state = new();
		InitializeHealth(state, health);
		state.Mana = mana;
		return state;
	}

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
	protected static void InitializeValue(ChildState target, int value) {
		target.Value = value;
	}

	public static ChildState Create(int value) {
		ChildState state = new();
		InitializeValue(state, value);
		return state;
	}

	[NetworkProperty]
	public partial int Value { get; private set; }
}

[NetworkObject]
public partial class ReplacementChildState : ChildState {
	public static ReplacementChildState Create(int value, int bonus) {
		ReplacementChildState state = new();
		InitializeValue(state, value);
		state.Bonus = bonus;
		return state;
	}

	[NetworkProperty]
	public partial int Bonus { get; private set; }
}

[NetworkObject]
public partial class ParentState : NetworkObject {
	public static ParentState Create(ChildState? child) {
		return new ParentState {
			Child = child
		};
	}

	[NetworkProperty]
	public partial ChildState? Child { get; private set; }
}

[NetworkObject]
public partial class ParentAssignmentState : NetworkObject {
	[NetworkProperty]
	public partial ChildState? Child { get; set; }

	[NetworkProperty]
	public partial ChildState? SecondaryChild { get; set; }
}

[NetworkObject]
public partial class DirtyPrimitiveState : NetworkObject {
	[NetworkProperty]
	public partial int Value { get; set; }
}

[NetworkObject]
public partial class DirtyPairState : NetworkObject {
	[NetworkProperty]
	public partial int First { get; set; }

	[NetworkProperty]
	public partial int Second { get; set; }
}

[NetworkObject]
public partial class DirtyChildState : NetworkObject {
	[NetworkProperty]
	public partial int Value { get; set; }
}

[NetworkObject]
public partial class DirtyParentState : NetworkObject {
	[NetworkProperty]
	public partial DirtyChildState? Child { get; set; }
}
