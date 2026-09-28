using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed class CloneNullChildTests {
	[Test]
	public void Clone_ClearedInitializedChildStaysNull() {
		CloneDefaultChildrenState source = new();
		source.Initialized = null;

		CloneDefaultChildrenState clone = source.Clone();

		Assert.Multiple(() => {
			Assert.That(clone.Initialized, Is.Null);
			Assert.That(clone.InitializedDefault, Is.Not.SameAs(source.InitializedDefault));
			AssertDetached(clone.InitializedDefault);
			Assert.That(source.Initialized, Is.Null);
			AssertDetached(source.InitializedDefault);
		});
	}

	[Test]
	public void Clone_ClearedConstructorChildDetachesFreshDefault() {
		CloneDefaultChildrenState source = new();
		AssertAttached(source.ConstructedDefault, source, nameof(source.Constructed));
		source.Constructed = null;
		NetworkPropertyState[] sourceStates = ((INetworkObject)source).PropertyStates.ToArray();
		int sourceChanges = 0;
		source.PropertyChanged += (_, _) => sourceChanges++;

		CloneDefaultChildrenState clone = source.Clone();

		Assert.Multiple(() => {
			Assert.That(clone.Constructed, Is.Null);
			Assert.That(clone.ConstructedDefault, Is.Not.SameAs(source.ConstructedDefault));
			AssertDetached(clone.ConstructedDefault);
			Assert.That(source.Constructed, Is.Null);
			AssertDetached(source.ConstructedDefault);
			Assert.That(((INetworkObject)source).PropertyStates, Is.EqualTo(sourceStates));
			Assert.That(sourceChanges, Is.Zero);
		});
	}

	[Test]
	public void Clone_ClearedNonNullableChildStaysNull() {
		CloneDefaultChildrenState source = new();
		source.NonNullable = null!;

		CloneDefaultChildrenState clone = source.Clone();

		Assert.Multiple(() => {
			Assert.That(clone.NonNullable, Is.Null);
			AssertDetached(clone.NonNullableDefault);
			Assert.That(source.NonNullable, Is.Null);
			AssertDetached(source.NonNullableDefault);
		});
	}

	[Test]
	public void Clone_InheritedNullChildrenStayNullThroughBaseReference() {
		CloneDefaultChildrenState source = new CloneDerivedDefaultChildrenState {
			Initialized = null,
			Constructed = null,
			NonNullable = null!,
			Level = 12
		};

		CloneDefaultChildrenState clone = source.Clone();

		Assert.That(clone, Is.TypeOf<CloneDerivedDefaultChildrenState>());
		Assert.Multiple(() => {
			Assert.That(clone, Is.Not.SameAs(source));
			Assert.That(((CloneDerivedDefaultChildrenState)clone).Level, Is.EqualTo(12));
			Assert.That(clone.Initialized, Is.Null);
			Assert.That(clone.Constructed, Is.Null);
			Assert.That(clone.NonNullable, Is.Null);
			AssertDetached(clone.InitializedDefault);
			AssertDetached(clone.ConstructedDefault);
			AssertDetached(clone.NonNullableDefault);
			Assert.That(source.Initialized, Is.Null);
			Assert.That(source.Constructed, Is.Null);
			Assert.That(source.NonNullable, Is.Null);
		});
	}

	[Test]
	public void Clone_NestedChildKeepsNullsAndDeepCopiesDerivedChildren() {
		ReplacementChildState grandchild = ReplacementChildState.Create(3, 7);
		CloneDerivedDefaultChildrenState child = new() {
			Initialized = null,
			Constructed = grandchild,
			NonNullable = null!,
			Level = 12
		};
		CloneDefaultChildContainer source = new() { Child = child };
		NetworkPropertyState[] sourceStates = ((INetworkObject)source).PropertyStates.ToArray();
		NetworkPropertyState[] childStates = ((INetworkObject)child).PropertyStates.ToArray();
		int sourceChanges = 0;
		source.PropertyChanged += (_, _) => sourceChanges++;

		CloneDefaultChildContainer clone = source.Clone();

		Assert.That(clone.Child, Is.TypeOf<CloneDerivedDefaultChildrenState>());
		CloneDerivedDefaultChildrenState clonedChild = (CloneDerivedDefaultChildrenState)clone.Child!;
		Assert.That(clonedChild.Constructed, Is.TypeOf<ReplacementChildState>());
		ReplacementChildState clonedGrandchild = (ReplacementChildState)clonedChild.Constructed!;
		Assert.Multiple(() => {
			Assert.That(clonedChild, Is.Not.SameAs(child));
			Assert.That(clonedChild.Level, Is.EqualTo(12));
			Assert.That(clonedChild.Initialized, Is.Null);
			Assert.That(clonedChild.NonNullable, Is.Null);
			Assert.That(clonedGrandchild, Is.Not.SameAs(grandchild));
			Assert.That(clonedGrandchild.Value, Is.EqualTo(3));
			Assert.That(clonedGrandchild.Bonus, Is.EqualTo(7));
			AssertAttached(clonedChild, clone, nameof(clone.Child));
			AssertAttached(clonedGrandchild, clonedChild, nameof(clonedChild.Constructed));
			AssertDetached(clone.ConstructedDefault);
			AssertDetached(clonedChild.InitializedDefault);
			AssertDetached(clonedChild.ConstructedDefault);
			AssertDetached(clonedChild.NonNullableDefault);
			Assert.That(source.Child, Is.SameAs(child));
			Assert.That(child.Constructed, Is.SameAs(grandchild));
			Assert.That(child.Initialized, Is.Null);
			Assert.That(child.NonNullable, Is.Null);
			AssertAttached(child, source, nameof(source.Child));
			AssertAttached(grandchild, child, nameof(child.Constructed));
			Assert.That(((INetworkObject)source).PropertyStates, Is.EqualTo(sourceStates));
			Assert.That(((INetworkObject)child).PropertyStates, Is.EqualTo(childStates));
			Assert.That(sourceChanges, Is.Zero);
		});
	}

	[Test]
	public void Clone_NonNullChildrenReplaceDefaultsWithIndependentAttachedCopies() {
		CloneDefaultChildrenState source = new() {
			Initialized = ReplacementChildState.Create(3, 7),
			Constructed = ChildState.Create(5),
			NonNullable = ChildState.Create(8)
		};

		CloneDefaultChildrenState clone = source.Clone();

		Assert.That(clone.Initialized, Is.TypeOf<ReplacementChildState>());
		Assert.Multiple(() => {
			Assert.That(clone.Initialized, Is.Not.SameAs(source.Initialized));
			Assert.That(clone.Initialized!.Value, Is.EqualTo(3));
			Assert.That(((ReplacementChildState)clone.Initialized).Bonus, Is.EqualTo(7));
			Assert.That(clone.Constructed, Is.Not.SameAs(source.Constructed));
			Assert.That(clone.Constructed!.Value, Is.EqualTo(5));
			Assert.That(clone.NonNullable, Is.Not.SameAs(source.NonNullable));
			Assert.That(clone.NonNullable.Value, Is.EqualTo(8));
			AssertAttached(clone.Initialized, clone, nameof(clone.Initialized));
			AssertAttached(clone.Constructed, clone, nameof(clone.Constructed));
			AssertAttached(clone.NonNullable, clone, nameof(clone.NonNullable));
			AssertDetached(clone.InitializedDefault);
			AssertDetached(clone.ConstructedDefault);
			AssertDetached(clone.NonNullableDefault);
			AssertAttached(source.Initialized!, source, nameof(source.Initialized));
			AssertAttached(source.Constructed!, source, nameof(source.Constructed));
			AssertAttached(source.NonNullable, source, nameof(source.NonNullable));
		});
	}

	[Test]
	public void Clone_NullChildWithoutDefaultStaysNull() {
		ParentState source = ParentState.Create(null);

		ParentState clone = source.Clone();

		Assert.Multiple(() => {
			Assert.That(clone, Is.Not.SameAs(source));
			Assert.That(clone.Child, Is.Null);
			Assert.That(source.Child, Is.Null);
			Assert.That(((INetworkObject)clone).PropertyStates, Is.All.EqualTo(NetworkPropertyState.Unchanged));
		});
	}

	private static void AssertDetached(NetworkObject child) {
		Assert.That(((INetworkObject)child).Parent, Is.Null);
		Assert.That(((INetworkObject)child).PropertyIndex, Is.EqualTo(-1));
		Assert.That(((INetworkObject)child).IsCollectionItem, Is.False);
	}

	private static void AssertAttached(NetworkObject child, NetworkObject parent, string propertyName) {
		int propertyIndex = ((INetworkObject)parent).NetworkProperties.Single(property => property.Name == propertyName).Index;
		Assert.That(((INetworkObject)child).Parent, Is.SameAs(parent));
		Assert.That(((INetworkObject)child).PropertyIndex, Is.EqualTo(propertyIndex));
		Assert.That(((INetworkObject)child).IsCollectionItem, Is.False);
	}
}
