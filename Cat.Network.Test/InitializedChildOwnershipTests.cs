using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed class InitializedChildOwnershipTests {
	[Test]
	public void InlineDefaultIsAttachedWithoutMarkingTheOwnerDirty() {
		InitializedChildState owner = new();

		AssertAttached(owner.Child!, owner, nameof(owner.Child));
		Assert.Multiple(() => {
			Assert.That(owner.Child!.Value, Is.EqualTo(7));
			Assert.That(owner.BeforeChild, Is.EqualTo(3));
			Assert.That(((INetworkObject)owner).PropertyStates, Is.All.EqualTo(NetworkPropertyState.Unchanged));
			Assert.That(((INetworkObject)owner.Child).PropertyStates, Is.All.EqualTo(NetworkPropertyState.Unchanged));
			Assert.That(owner.Children.Owner, Is.SameAs(owner));
		});
	}

	[TestCase(false)]
	[TestCase(true)]
	public void MutatingDefaultChildMarksOwnerModifiedAndRaisesItsEvent(bool clearFirst) {
		InitializedChildState owner = new();
		if (clearFirst) {
			ClearDirtyState(owner);
		}
		List<PropertyChangedEventArgs> events = [];
		owner.PropertyChanged += (_, args) => events.Add(args);

		owner.Child!.Value = 42;

		Assert.Multiple(() => {
			Assert.That(StateOf(owner, nameof(owner.Child)), Is.EqualTo(NetworkPropertyState.Modified));
			Assert.That(StateOf(owner.Child, nameof(owner.Child.Value)), Is.EqualTo(NetworkPropertyState.Replaced));
			Assert.That(events.Select(args => args.Name), Is.EqualTo(new[] { nameof(owner.Child) }));
		});
	}

	[Test]
	public void NestedAndPrivateInheritedDefaultsAttachToTheMostDerivedOwner() {
		InitializedChildDerivedState owner = new();
		InitializedChildState branch = owner.GetBaseChild();
		InitializedChildLeaf leaf = branch.Child!;

		AssertAttached(branch, owner, "BaseChild");
		AssertAttached(leaf, branch, nameof(branch.Child));
		AssertAttached(owner.DerivedChild, owner, nameof(owner.DerivedChild));
		Assert.Multiple(() => {
			Assert.That(owner.BaseConstructorSawAttachedChild, Is.True);
			Assert.That(owner.BaseConstructorSawCollectionOwner, Is.True);
			Assert.That(leaf.Anchor, Is.SameAs(owner));
			Assert.That(leaf.IsOwner, Is.EqualTo(owner.IsOwner));
			Assert.That(((INetworkObject)owner).PropertyStates, Is.All.EqualTo(NetworkPropertyState.Unchanged));
		});

		ClearDirtyState(owner);
		List<string> changedNames = [];
		owner.PropertyChanged += (_, args) => changedNames.Add(args.Name);
		leaf.Value = 42;
		owner.DerivedChild.Value = 43;
		owner.GetBaseValues().Add(9);

		Assert.Multiple(() => {
			Assert.That(StateOf(branch, nameof(branch.Child)), Is.EqualTo(NetworkPropertyState.Modified));
			Assert.That(StateOf(owner, "BaseChild"), Is.EqualTo(NetworkPropertyState.Modified));
			Assert.That(StateOf(owner, "BaseValues"), Is.EqualTo(NetworkPropertyState.Modified));
			Assert.That(StateOf(owner, nameof(owner.DerivedChild)), Is.EqualTo(NetworkPropertyState.Modified));
			Assert.That(changedNames, Does.Contain("BaseChild").And.Contain(nameof(owner.DerivedChild)));
		});
	}

	[Test]
	public void DefaultIsAttachedBeforeItsConstructorBodyMutatesIt() {
		InitializedChildConstructorState owner = new();

		AssertAttached(owner.Child, owner, nameof(owner.Child));
		Assert.Multiple(() => {
			Assert.That(owner.DefaultWasAttached, Is.True);
			Assert.That(owner.StateBeforeMutation, Is.EqualTo(NetworkPropertyState.Unchanged));
			Assert.That(owner.Child.Value, Is.EqualTo(15));
			Assert.That(StateOf(owner, nameof(owner.Child)), Is.EqualTo(NetworkPropertyState.Modified));
			Assert.That(owner.ChangeCount, Is.EqualTo(1));
		});
	}

	[Test]
	public void ConstructorAssignedChildStillUsesTheSetter() {
		ConstructorAssignedChildState owner = new();

		AssertAttached(owner.Child!, owner, nameof(owner.Child));
		Assert.That(StateOf(owner, nameof(owner.Child)), Is.EqualTo(NetworkPropertyState.Replaced));
	}

	[TestCase(false)]
	[TestCase(true)]
	public void ConstructorCanReplaceOrClearAnAttachedDefault(bool clearDefault) {
		InitializedChildLeaf? replacement = clearDefault ? null : new();
		ConstructorReplacedChildState owner = ConstructorReplacedChildState.Create(replacement);

		AssertDetached(owner.OriginalChild);
		Assert.Multiple(() => {
			Assert.That(owner.DefaultWasAttached, Is.True);
			Assert.That(owner.Child, Is.SameAs(replacement));
			Assert.That(owner.ChangeCount, Is.EqualTo(1));
			Assert.That(StateOf(owner, nameof(owner.Child)), Is.EqualTo(NetworkPropertyState.Replaced));
		});
		if (replacement is not null) {
			AssertAttached(replacement, owner, nameof(owner.Child));
		}

		Array.Clear(((INetworkObject)owner).PropertyStates);
		owner.OriginalChild.Value = 42;
		Assert.That(StateOf(owner, nameof(owner.Child)), Is.EqualTo(NetworkPropertyState.Unchanged));
	}

	[Test]
	public void ReplacingAndClearingDefaultDetachesPreviousChildren() {
		InitializedChildState owner = new();
		InitializedChildLeaf original = owner.Child!;
		InitializedChildLeaf replacement = new();
		owner.Child = replacement;

		AssertDetached(original);
		AssertAttached(replacement, owner, nameof(owner.Child));
		ClearDirtyState(owner);
		original.Value = 41;
		Assert.That(StateOf(owner, nameof(owner.Child)), Is.EqualTo(NetworkPropertyState.Unchanged));
		replacement.Value = 42;
		Assert.That(StateOf(owner, nameof(owner.Child)), Is.EqualTo(NetworkPropertyState.Modified));

		owner.Child = null;
		AssertDetached(replacement);
		ClearDirtyState(owner);
		replacement.Value = 43;
		Assert.That(StateOf(owner, nameof(owner.Child)), Is.EqualTo(NetworkPropertyState.Unchanged));
	}

	[Test]
	public void NullDefaultsStayNullAndCanBeAssignedLater() {
		InitializedChildState owner = new();
		InitializedChildPairState pair = new();
		Assert.Multiple(() => {
			Assert.That(owner.NullChild, Is.Null);
			Assert.That(owner.SuppressedNullChild, Is.Null);
			Assert.That(pair.First, Is.Null);
			Assert.That(pair.Second, Is.Null);
			Assert.That(((INetworkObject)pair).PropertyStates, Is.All.EqualTo(NetworkPropertyState.Unchanged));
		});

		owner.NullChild = new();
		AssertAttached(owner.NullChild, owner, nameof(owner.NullChild));
		Assert.That(StateOf(owner, nameof(owner.NullChild)), Is.EqualTo(NetworkPropertyState.Replaced));
	}

	[Test]
	public void DefaultChildCannotBeAssignedToAnotherOwnerOrCollection() {
		InitializedChildState firstOwner = new();
		InitializedChildState secondOwner = new();
		InitializedChildLeaf secondDefault = secondOwner.Child!;

		Assert.That(() => secondOwner.Child = firstOwner.Child, Throws.TypeOf<InvalidOperationException>());
		Assert.That(() => secondOwner.Children.Add(firstOwner.Child!), Throws.TypeOf<InvalidOperationException>());
		AssertAttached(firstOwner.Child!, firstOwner, nameof(firstOwner.Child));
		AssertAttached(secondDefault, secondOwner, nameof(secondOwner.Child));
		Assert.Multiple(() => {
			Assert.That(secondOwner.Child, Is.SameAs(secondDefault));
			Assert.That(secondOwner.Children, Is.Empty);
			Assert.That(((INetworkObject)secondOwner).PropertyStates, Is.All.EqualTo(NetworkPropertyState.Unchanged));
		});
	}

	[Test]
	public void SharedDefaultBetweenPropertiesIsRejectedBeforeAttachingAnyChild() {
		InitializedChildLeaf child = new();

		Assert.That(() => InitializedChildPairState.Create(child, child), Throws.TypeOf<InvalidOperationException>());
		AssertDetached(child);
	}

	[TestCase(false)]
	[TestCase(true)]
	public void OwnedDefaultIsRejectedBeforeAttachingOtherDefaults(bool collectionItem) {
		InitializedChildState owner = new();
		InitializedChildLeaf owned = owner.Child!;
		if (collectionItem) {
			owned = new();
			owner.Children.Add(owned);
		}
		InitializedChildLeaf available = new();
		NetworkPropertyState[] ownerStates = ((INetworkObject)owner).PropertyStates.ToArray();
		int originalIndex = ((INetworkObject)owned).PropertyIndex;

		Assert.That(() => InitializedChildPairState.Create(available, owned), Throws.TypeOf<InvalidOperationException>());
		AssertDetached(available);
		Assert.Multiple(() => {
			Assert.That(((INetworkObject)owned).Parent, Is.SameAs(owner));
			Assert.That(((INetworkObject)owned).PropertyIndex, Is.EqualTo(originalIndex));
			Assert.That(((INetworkObject)owned).IsCollectionItem, Is.EqualTo(collectionItem));
			Assert.That(((INetworkObject)owner).PropertyStates, Is.EqualTo(ownerStates));
		});
	}

	[Test]
	public void DistinctEqualDefaultChildrenHaveIndependentOwnershipSlots() {
		InitializedChildLeaf first = new();
		InitializedChildLeaf second = new();
		Assert.That(first.Equals(second), Is.True);

		InitializedChildPairState owner = InitializedChildPairState.Create(first, second);

		AssertAttached(first, owner, nameof(owner.First));
		AssertAttached(second, owner, nameof(owner.Second));
		first.Value = 42;
		Assert.Multiple(() => {
			Assert.That(StateOf(owner, nameof(owner.First)), Is.EqualTo(NetworkPropertyState.Modified));
			Assert.That(StateOf(owner, nameof(owner.Second)), Is.EqualTo(NetworkPropertyState.Unchanged));
		});
	}

	[Test]
	public void ReinitializingTheSamePropertySlotKeepsItsAttachmentAndRaisesNoEvents() {
		InitializedChildState owner = new();
		InitializedChildLeaf child = owner.Child!;
		int eventCount = 0;
		owner.PropertyChanged += (_, _) => eventCount++;
		owner.ChildChanged += (_, _) => eventCount++;

		((INetworkObject)owner).Initialize();

		AssertAttached(child, owner, nameof(owner.Child));
		Assert.Multiple(() => {
			Assert.That(eventCount, Is.Zero);
			Assert.That(owner.Child, Is.SameAs(child));
			Assert.That(((INetworkObject)owner).PropertyStates, Is.All.EqualTo(NetworkPropertyState.Unchanged));
		});
	}

	[TestCase(false)]
	[TestCase(true)]
	public void InitializationRejectsAncestorOrCyclicOwnerBeforeChangingState(bool cyclicOwner) {
		InitializedChildState owner = new();
		InitializedChildLeaf child = owner.Child!;
		INetworkObject networkOwner = owner;
		// The interface permits constructing an invalid owner chain without entering a setter's dirty walk.
		networkOwner.Parent = cyclicOwner ? owner : child;
		networkOwner.PropertyStates[0] = NetworkPropertyState.Modified;
		NetworkPropertyState[] beforeStates = networkOwner.PropertyStates;
		NetworkObject? beforeChildParent = ((INetworkObject)child).Parent;
		try {
			Assert.That(networkOwner.Initialize, Throws.TypeOf<InvalidOperationException>());
			Assert.Multiple(() => {
				Assert.That(networkOwner.PropertyStates, Is.SameAs(beforeStates));
				Assert.That(networkOwner.PropertyStates[0], Is.EqualTo(NetworkPropertyState.Modified));
				Assert.That(((INetworkObject)child).Parent, Is.SameAs(beforeChildParent));
			});
		} finally {
			networkOwner.Parent = null;
		}
	}

	[TestCase(MemberIdentificationMode.Index)]
	[TestCase(MemberIdentificationMode.Name)]
	public void FullAndDirtyRoundTripsPreserveInitializedChildOwnership(MemberIdentificationMode identificationMode) {
		InitializedChildDerivedState source = new();
		InitializedChildDerivedState target = new();
		InitializedChildState displacedDefault = target.GetBaseChild();
		source.GetBaseChild().Child!.Value = 41;
		source.DerivedChild.Value = 43;
		source.GetBaseValues().Add(9);
		TypeCatalogue catalogue = CreateCatalogue();
		SerializationContext context = new(catalogue);
		INetworkObjectSerializer serializer = SerializerFor(source, catalogue);

		byte[] fullPayload = Serialize(source, serializer, context, MemberSelectionMode.All, identificationMode);
		serializer.Deserialize(target, fullPayload, context);
		AssertDetached(displacedDefault);
		AssertAttached(target.GetBaseChild(), target, "BaseChild");
		AssertAttached(target.GetBaseChild().Child!, target.GetBaseChild(), nameof(InitializedChildState.Child));
		Assert.That(Serialize(target, serializer, context, MemberSelectionMode.All, identificationMode), Is.EqualTo(fullPayload));
		serializer.ClearDirtyState(source, context);
		serializer.ClearDirtyState(target, context);
		InitializedChildState targetBranch = target.GetBaseChild();
		InitializedChildLeaf targetLeaf = targetBranch.Child!;

		source.GetBaseChild().Child!.Value = 42;
		byte[] dirtyPayload = Serialize(source, serializer, context, MemberSelectionMode.Dirty, identificationMode);
		serializer.Deserialize(target, dirtyPayload, context);

		Assert.Multiple(() => {
			Assert.That(target.GetBaseChild(), Is.SameAs(targetBranch));
			Assert.That(targetBranch.Child, Is.SameAs(targetLeaf));
			Assert.That(targetLeaf.Value, Is.EqualTo(42));
			Assert.That(target.DerivedChild.Value, Is.EqualTo(43));
			Assert.That(target.GetBaseValues(), Is.EqualTo(new[] { 9 }));
		});
		serializer.ClearDirtyState(target, context);
		targetLeaf.Value = 44;
		Assert.That(StateOf(target, "BaseChild"), Is.EqualTo(NetworkPropertyState.Modified));
	}

	private static void AssertAttached(NetworkObject child, NetworkObject owner, string propertyName) {
		INetworkObject networkChild = child;
		Assert.Multiple(() => {
			Assert.That(networkChild.Parent, Is.SameAs(owner));
			Assert.That(networkChild.PropertyIndex, Is.EqualTo(PropertyIndexOf(owner, propertyName)));
			Assert.That(networkChild.IsCollectionItem, Is.False);
		});
	}

	private static void AssertDetached(NetworkObject child) {
		INetworkObject networkChild = child;
		Assert.Multiple(() => {
			Assert.That(networkChild.Parent, Is.Null);
			Assert.That(networkChild.PropertyIndex, Is.EqualTo(-1));
			Assert.That(networkChild.IsCollectionItem, Is.False);
		});
	}

	private static int PropertyIndexOf(NetworkObject owner, string propertyName) =>
		((INetworkObject)owner).NetworkProperties.Single(property => property.Name == propertyName).Index;

	private static NetworkPropertyState StateOf(NetworkObject owner, string propertyName) =>
		((INetworkObject)owner).PropertyStates[PropertyIndexOf(owner, propertyName)];

	private static TypeCatalogue CreateCatalogue() {
		TypeCatalogue catalogue = new();
		catalogue.Register(typeof(InitializedChildLeaf));
		catalogue.Register(typeof(InitializedChildState));
		catalogue.Register(typeof(InitializedChildDerivedState));
		return catalogue;
	}

	private static INetworkObjectSerializer SerializerFor(NetworkObject target, TypeCatalogue catalogue) {
		Assert.That(catalogue.TryFindSerializer(target.GetType(), out INetworkObjectSerializer? serializer), Is.True);
		return serializer!;
	}

	private static void ClearDirtyState(NetworkObject target) {
		TypeCatalogue catalogue = CreateCatalogue();
		SerializerFor(target, catalogue).ClearDirtyState(target, new(catalogue));
	}

	private static byte[] Serialize(NetworkObject target, INetworkObjectSerializer serializer, SerializationContext context,
		MemberSelectionMode selectionMode, MemberIdentificationMode identificationMode) {
		BufferWriter writer = new();
		serializer.Serialize(writer, target, context, new(selectionMode, identificationMode));
		return writer.GetWrittenSpan().ToArray();
	}
}
