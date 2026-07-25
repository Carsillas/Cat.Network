namespace Cat.Network.Test.Entities;

[NetworkObject]
public partial class PrimitiveState : NetworkObject {
	public PrimitiveState() {
	}

	public PrimitiveState(int health, string name) {
		Health = health;
		Name = name;
	}

	[NetworkProperty]
	public partial int Health { get; private set; }

	[NetworkProperty]
	public partial string Name { get; private set; } = string.Empty;
}

[NetworkObject]
public partial class NullableState : NetworkObject {
	public NullableState() {
	}

	public NullableState(int? health, Guid? sessionId) {
		Health = health;
		SessionId = sessionId;
	}

	[NetworkProperty]
	public partial int? Health { get; private set; }

	[NetworkProperty]
	public partial Guid? SessionId { get; private set; }
}

[NetworkObject]
public partial class ActorState : NetworkObject {
	public ActorState() {
	}

	protected ActorState(int health) {
		Health = health;
	}

	[NetworkProperty]
	public partial int Health { get; private set; }
}

[NetworkObject]
public partial class PlayerState : ActorState {
	public PlayerState() {
	}

	public PlayerState(int health, int mana) : base(health) {
		Mana = mana;
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
	public ChildState() {
	}

	public ChildState(int value) {
		Value = value;
	}

	[NetworkProperty]
	public partial int Value { get; private set; }
}

[NetworkObject]
public partial class ReplacementChildState : ChildState {
	public ReplacementChildState() {
	}

	public ReplacementChildState(int value, int bonus) : base(value) {
		Bonus = bonus;
	}

	[NetworkProperty]
	public partial int Bonus { get; private set; }
}

[NetworkObject]
public partial class ParentState : NetworkObject {
	public ParentState() {
	}

	public ParentState(ChildState? child) {
		Child = child;
	}

	[NetworkProperty]
	public partial ChildState? Child { get; private set; }
}
