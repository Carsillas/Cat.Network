using System.Buffers.Binary;
using System.Text;
using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	[Test]
	public void Assigning_NetworkObjectProperty_SetsAndClearsParent() {
		ParentAssignmentState parent = new();
		ChildState firstChild = ChildState.Create(1);
		ChildState secondChild = ChildState.Create(2);

		parent.Child = firstChild;
		Assert.Multiple(() => {
			Assert.That(((INetworkObject)firstChild).Parent, Is.SameAs(parent));
			Assert.That(((INetworkObject)firstChild).PropertyIndex, Is.EqualTo(0));
		});

		parent.Child = secondChild;

		Assert.Multiple(() => {
			Assert.That(((INetworkObject)firstChild).Parent, Is.Null);
			Assert.That(((INetworkObject)firstChild).PropertyIndex, Is.EqualTo(-1));
			Assert.That(((INetworkObject)secondChild).Parent, Is.SameAs(parent));
			Assert.That(((INetworkObject)secondChild).PropertyIndex, Is.EqualTo(0));
		});

		parent.Child = null;

		Assert.Multiple(() => {
			Assert.That(((INetworkObject)secondChild).Parent, Is.Null);
			Assert.That(((INetworkObject)secondChild).PropertyIndex, Is.EqualTo(-1));
		});
	}

	[Test]
	public void Assigning_NetworkObjectProperty_ThrowsWhenChildAlreadyHasParent() {
		ParentAssignmentState firstParent = new();
		ParentAssignmentState secondParent = new();
		ChildState child = ChildState.Create(1);

		firstParent.Child = child;

		Assert.That(() => secondParent.Child = child, Throws.TypeOf<InvalidOperationException>());
		Assert.That(((INetworkObject)child).Parent, Is.SameAs(firstParent));
		Assert.That(secondParent.Child, Is.Null);
	}

	[Test]
	public void Assigning_NetworkObjectProperty_ThrowsWhenChildAlreadyOccupiesDifferentPropertyOnSameParent() {
		ParentAssignmentState parent = new();
		ChildState child = ChildState.Create(1);

		parent.Child = child;

		Assert.That(() => parent.SecondaryChild = child, Throws.TypeOf<InvalidOperationException>());

		Assert.Multiple(() => {
			Assert.That(parent.Child, Is.SameAs(child));
			Assert.That(parent.SecondaryChild, Is.Null);
			Assert.That(((INetworkObject)child).Parent, Is.SameAs(parent));
			Assert.That(((INetworkObject)child).PropertyIndex, Is.EqualTo(0));
		});
	}

	[Test]
	public void Setting_Property_MarksOwnStateAsReplaced() {
		DirtyPrimitiveState target = new();

		target.Value = 9;

		Assert.That(((INetworkObject)target).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Replaced));
	}

	[Test]
	public void Setting_Property_RaisesPropertyChangedEvents() {
		DirtyPrimitiveState target = new();
		object? propertyChangedSender = null;
		PropertyChangedEventArgs? propertyChangedArgs = null;
		DirtyPrimitiveState? valueChangedSender = null;
		PropertyChangedEventArgs<int>? valueChangedArgs = null;
		int propertyChangedCount = 0;
		int valueChangedCount = 0;

		target.PropertyChanged += (sender, args) => {
			propertyChangedSender = sender;
			propertyChangedArgs = args;
			propertyChangedCount++;
		};
		target.ValueChanged += (sender, args) => {
			valueChangedSender = sender;
			valueChangedArgs = args;
			valueChangedCount++;
		};

		target.Value = 9;
		target.Value = 9;

		Assert.Multiple(() => {
			Assert.That(propertyChangedSender, Is.SameAs(target));
			Assert.That(propertyChangedArgs.HasValue, Is.True);
			Assert.That(propertyChangedArgs!.Value.Index, Is.EqualTo(0));
			Assert.That(propertyChangedArgs.Value.Name, Is.EqualTo(nameof(DirtyPrimitiveState.Value)));
			Assert.That(propertyChangedCount, Is.EqualTo(1));
			Assert.That(valueChangedSender, Is.SameAs(target));
			Assert.That(valueChangedArgs.HasValue, Is.True);
			Assert.That(valueChangedArgs!.Value.Index, Is.EqualTo(0));
			Assert.That(valueChangedArgs.Value.Name, Is.EqualTo(nameof(DirtyPrimitiveState.Value)));
			Assert.That(valueChangedArgs.Value.PreviousValue, Is.EqualTo(0));
			Assert.That(valueChangedArgs.Value.CurrentValue, Is.EqualTo(9));
			Assert.That(valueChangedCount, Is.EqualTo(1));
		});
	}

	[Test]
	public void Setting_AnchoredChildProperty_RaisesPropertyChangedOnAnchor() {
		PropertyChangedEntityState entity = new();
		DirtyChildState child = new();
		PropertyChangedEventArgs? entityArgs = null;

		entity.Child = child;
		entity.PropertyChanged += (_, args) => {
			entityArgs = args;
		};

		child.Value = 3;

		Assert.Multiple(() => {
			Assert.That(entity.Anchor, Is.SameAs(entity));
			Assert.That(child.Anchor, Is.SameAs(entity));
			Assert.That(child.IsOwner, Is.EqualTo(entity.IsOwner));
			Assert.That(entityArgs.HasValue, Is.True);
			Assert.That(entityArgs!.Value.Index, Is.EqualTo(1));
			Assert.That(entityArgs.Value.Name, Is.EqualTo(nameof(PropertyChangedEntityState.Child)));
		});
	}

	[Test]
	public void Setting_ChildProperty_MarksParentStateAsModified() {
		DirtyParentState parent = new();
		DirtyChildState child = new();

		parent.Child = child;
		((INetworkObject)parent).PropertyStates[0] = NetworkPropertyState.Unchanged;

		child.Value = 9;

		Assert.Multiple(() => {
			Assert.That(((INetworkObject)child).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Replaced));
			Assert.That(((INetworkObject)parent).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Modified));
		});
	}

	[Test]
	public void Setting_ChildProperty_MarksAllAncestorsAsModified() {
		DirtyParentState grandParent = new();
		DirtyParentState parent = new();
		DirtyChildState child = new();

		((INetworkObject)parent).Parent = grandParent;
		((INetworkObject)parent).PropertyIndex = 0;
		parent.Child = child;
		((INetworkObject)grandParent).PropertyStates = new[] { NetworkPropertyState.Unchanged };
		((INetworkObject)parent).PropertyStates[0] = NetworkPropertyState.Unchanged;

		child.Value = 5;

		Assert.Multiple(() => {
			Assert.That(((INetworkObject)child).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Replaced));
			Assert.That(((INetworkObject)parent).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Modified));
			Assert.That(((INetworkObject)grandParent).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Modified));
		});
	}
}
