using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	[Test]
	public void Equality_PrimitiveState_UsesNetworkPropertiesOnly() {
		PrimitiveState first = PrimitiveState.Create(42, "Mira");
		PrimitiveState second = PrimitiveState.Create(42, "Mira");
		second.IgnoredValue = 99;

		Assert.Multiple(() => {
			Assert.That(first.Equals(second), Is.True);
			Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
		});
	}

	[Test]
	public void Equality_NestedNetworkObjectsAndCollections_AreStructural() {
		ObjectDictionaryState first = new();
		first.Children.Add(6, new DirtyChildState { Value = 8 });
		first.Children.Add(2, new DirtyChildState { Value = 4 });

		ObjectDictionaryState second = new();
		second.Children.Add(2, new DirtyChildState { Value = 4 });
		second.Children.Add(6, new DirtyChildState { Value = 8 });

		Assert.Multiple(() => {
			Assert.That(first.Equals(second), Is.True);
			Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
		});
	}

	[Test]
	public void Equality_IgnoresParentAndDirtyTrackingState() {
		DirtyParentState first = new() {
			Child = new DirtyChildState { Value = 5 }
		};
		DirtyParentState second = new() {
			Child = new DirtyChildState { Value = 5 }
		};

		((INetworkObject)first).PropertyStates[0] = NetworkPropertyState.Unchanged;
		((INetworkObject)second).PropertyStates[0] = NetworkPropertyState.Modified;

		Assert.Multiple(() => {
			Assert.That(((INetworkObject)first.Child!).Parent, Is.SameAs(first));
			Assert.That(((INetworkObject)second.Child!).Parent, Is.SameAs(second));
			Assert.That(first.Equals(second), Is.True);
			Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
		});
	}

	[Test]
	public void Equality_UsesInheritedNetworkMembers() {
		PlayerState first = PlayerState.Create(30, 12);
		PlayerState second = PlayerState.Create(30, 12);
		PlayerState different = PlayerState.Create(31, 12);

		Assert.Multiple(() => {
			Assert.That(first.Equals(second), Is.True);
			Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
			Assert.That(first.Equals(different), Is.False);
		});
	}
}
