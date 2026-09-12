namespace Cat.Network.Test.Entities;

[NetworkObject]
public partial class InitializedChildLeaf : NetworkObject {
	[NetworkProperty]
	public partial int Value { get; set; } = 7;
}

[NetworkObject]
public partial class InitializedChildState : NetworkObject {
	[NetworkProperty]
	public partial int BeforeChild { get; set; } = 3;

	[NetworkProperty]
	public partial InitializedChildLeaf? Child { get; set; } = new();

	[NetworkCollection]
	public partial NetworkList<InitializedChildLeaf> Children { get; }

	[NetworkProperty]
	public partial InitializedChildLeaf? NullChild { get; set; } = null;

	[NetworkProperty]
	public partial InitializedChildLeaf SuppressedNullChild { get; set; } = null!;
}

[NetworkObject]
public abstract partial class InitializedChildBaseState : NetworkEntity {
	protected InitializedChildBaseState() {
		BaseConstructorSawAttachedChild = ReferenceEquals(((INetworkObject)BaseChild).Parent, this);
		BaseConstructorSawCollectionOwner = ReferenceEquals(BaseValues.Owner, this);
	}

	[NetworkProperty]
	private partial InitializedChildState BaseChild { get; set; } = new();

	[NetworkCollection]
	private partial NetworkList<int> BaseValues { get; }

	public InitializedChildState GetBaseChild() => BaseChild;

	public NetworkList<int> GetBaseValues() => BaseValues;

	public bool BaseConstructorSawAttachedChild { get; }
	public bool BaseConstructorSawCollectionOwner { get; }
}

[NetworkObject]
public partial class InitializedChildDerivedState : InitializedChildBaseState {
	[NetworkProperty]
	public partial InitializedChildLeaf DerivedChild { get; set; } = new();
}

[NetworkObject]
public partial class InitializedChildConstructorState : NetworkObject {
	public InitializedChildConstructorState() {
		DefaultWasAttached = ReferenceEquals(((INetworkObject)Child).Parent, this);
		StateBeforeMutation = ((INetworkObject)this).PropertyStates[0];
		PropertyChanged += (_, _) => ChangeCount++;
		Child.Value = 15;
	}

	[NetworkProperty]
	public partial InitializedChildLeaf Child { get; set; } = new();

	public bool DefaultWasAttached { get; }
	public NetworkPropertyState StateBeforeMutation { get; }
	public int ChangeCount { get; private set; }
}

[NetworkObject]
public partial class ConstructorAssignedChildState : NetworkObject {
	public ConstructorAssignedChildState() {
		Child = new InitializedChildLeaf();
	}

	[NetworkProperty]
	public partial InitializedChildLeaf? Child { get; set; }
}

[NetworkObject]
public partial class ConstructorReplacedChildState : NetworkObject {
	[ThreadStatic]
	private static InitializedChildLeaf? replacementDefault;

	public ConstructorReplacedChildState() {
		OriginalChild = Child!;
		DefaultWasAttached = ReferenceEquals(((INetworkObject)OriginalChild).Parent, this);
		ChildChanged += (_, _) => ChangeCount++;
		Child = replacementDefault;
	}

	public static ConstructorReplacedChildState Create(InitializedChildLeaf? replacement) {
		InitializedChildLeaf? previous = replacementDefault;
		try {
			replacementDefault = replacement;
			return new();
		} finally {
			replacementDefault = previous;
		}
	}

	[NetworkProperty]
	public partial InitializedChildLeaf? Child { get; set; } = new();

	public InitializedChildLeaf OriginalChild { get; }
	public bool DefaultWasAttached { get; }
	public int ChangeCount { get; private set; }
}

[NetworkObject]
public partial class InitializedChildPairState : NetworkObject {
	[ThreadStatic]
	private static InitializedChildLeaf? firstDefault;

	[ThreadStatic]
	private static InitializedChildLeaf? secondDefault;

	[NetworkProperty]
	public partial InitializedChildLeaf? First { get; set; } = firstDefault;

	[NetworkProperty]
	public partial InitializedChildLeaf? Second { get; set; } = secondDefault;

	public static InitializedChildPairState Create(InitializedChildLeaf? first, InitializedChildLeaf? second) {
		(InitializedChildLeaf? First, InitializedChildLeaf? Second) previous = (firstDefault, secondDefault);
		try {
			firstDefault = first;
			secondDefault = second;
			return new();
		} finally {
			(firstDefault, secondDefault) = previous;
		}
	}
}
