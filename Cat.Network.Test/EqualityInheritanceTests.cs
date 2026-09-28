using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed class EqualityInheritanceTests {
	[Test]
	public void ConcreteBaseAndDerived_AreUnequalInBothDirections() {
		EqualityBaseState baseValue = new();
		baseValue.SetBaseValue(1);
		EqualityDerivedState derived = new() { DerivedValue = 2 };
		derived.SetBaseValue(1);

		AssertEquality(baseValue, derived, false);
	}

	[Test]
	public void SiblingDerivedTypes_AreUnequalWithMatchingProperties() {
		EqualityDerivedState first = new() { DerivedValue = 2 };
		EqualitySiblingState second = new() { DerivedValue = 2 };
		first.SetBaseValue(1);
		second.SetBaseValue(1);

		AssertEquality(first, second, false);
	}

	[Test]
	public void BaseTypedEquality_IncludesDerivedProperties() {
		EqualityDerivedState first = new() { DerivedValue = 2 };
		EqualityDerivedState second = new() { DerivedValue = 3 };

		Assert.That(first.Equals(second), Is.False);
		AssertEquality(first, second, false);
	}

	[Test]
	public void ThreeLevelHierarchy_EqualValuesHaveEqualHashes() {
		EqualityLeafState first = CreateLeaf();
		EqualityLeafState second = CreateLeaf();
		second.IgnoredValue = 99;
		((INetworkObject)first).PropertyStates[0] = NetworkPropertyState.Unchanged;
		((INetworkObject)second).PropertyStates[0] = NetworkPropertyState.Modified;

		Assert.That(first.Equals(second), Is.True);
		AssertEquality(first, second, true);
		Assert.That(((IEquatable<EqualityDerivedState>)first).Equals(second), Is.True);
	}

	[TestCase("base property")]
	[TestCase("middle property")]
	[TestCase("leaf property")]
	[TestCase("base collection")]
	[TestCase("middle collection")]
	[TestCase("leaf collection")]
	public void ThreeLevelHierarchy_AllMembersAffectBaseTypedEquality(string changedMember) {
		EqualityLeafState first = CreateLeaf();
		EqualityLeafState second = CreateLeaf();
		switch (changedMember) {
			case "base property": second.SetBaseValue(99); break;
			case "middle property": second.DerivedValue = 99; break;
			case "leaf property": second.LeafValue = "changed"; break;
			case "base collection": second.SetBaseItem(99); break;
			case "middle collection": second.SetDerivedItem(2, 99); break;
			case "leaf collection": second.LeafValues[0] = 99; break;
		}

		Assert.That(first.Equals(second), Is.False);
		AssertEquality(first, second, false);
		Assert.Multiple(() => {
			Assert.That(((IEquatable<EqualityDerivedState>)first).Equals(second), Is.False);
			Assert.That(EqualityComparer<EqualityDerivedState>.Default.Equals(first, second), Is.False);
		});
	}

	[Test]
	public void EmptyDerivedType_StillIncludesInheritedState() {
		EqualityEmptyLeafState first = new() { DerivedValue = 2 };
		EqualityEmptyLeafState second = new() { DerivedValue = 2 };
		AssertEquality(first, second, true);

		second.DerivedValue = 3;
		AssertEquality(first, second, false);
		AssertEquality(first, new EqualityDerivedState { DerivedValue = 2 }, false);
	}

	[Test]
	public void AbstractBaseComparer_IncludesConcreteProperties() {
		EqualityConcreteState first = new() { AbstractValue = 1, ConcreteValue = 2 };
		EqualityConcreteState equal = new() { AbstractValue = 1, ConcreteValue = 2 };
		EqualityConcreteState different = new() { AbstractValue = 1, ConcreteValue = 3 };
		EqualityComparer<EqualityAbstractState> comparer = EqualityComparer<EqualityAbstractState>.Default;

		Assert.Multiple(() => {
			Assert.That(comparer.Equals(first, equal), Is.True);
			Assert.That(comparer.GetHashCode(first), Is.EqualTo(comparer.GetHashCode(equal)));
			Assert.That(comparer.Equals(first, different), Is.False);
			Assert.That(((IEquatable<EqualityAbstractState>)first).Equals(different), Is.False);
		});
	}

	[Test]
	public void DerivedClone_EqualsSourceThroughEveryBaseType() {
		EqualityLeafState source = CreateLeaf(withCollections: false);
		EqualityLeafState clone = source.Clone();

		Assert.That(clone, Is.Not.SameAs(source));
		AssertEquality(source, clone, true);
		Assert.Multiple(() => {
			Assert.That(source.Equals(clone), Is.True);
			Assert.That(((IEquatable<EqualityDerivedState>)source).Equals(clone), Is.True);
		});

		clone.LeafValue = "changed";
		AssertEquality(source, clone, false);
	}

	[Test]
	public void BaseTypedHashSet_DeduplicatesEqualDerivedValues() {
		EqualityLeafState first = CreateLeaf();
		EqualityLeafState equal = CreateLeaf();
		EqualityLeafState different = CreateLeaf();
		different.LeafValue = "changed";
		HashSet<EqualityBaseState> values = new() { first };

		Assert.Multiple(() => {
			Assert.That(values.Comparer.Equals(first, equal), Is.True);
			Assert.That(values.Comparer.Equals(first, different), Is.False);
			Assert.That(values.Contains(equal), Is.True);
			Assert.That(values.Contains(different), Is.False);
			Assert.That(values.Add(equal), Is.False);
			Assert.That(values.Add(different), Is.True);
			Assert.That(values, Has.Count.EqualTo(2));
		});
	}

	[TestCase("property")]
	[TestCase("list")]
	[TestCase("dictionary")]
	public void BaseTypedNestedMembers_UseDerivedEquality(string memberKind) {
		EqualityContainerState first = CreateContainer(memberKind, 2);
		EqualityContainerState equal = CreateContainer(memberKind, 2);
		EqualityContainerState different = CreateContainer(memberKind, 3);

		Assert.Multiple(() => {
			Assert.That(first.Equals(equal), Is.True);
			Assert.That(first.GetHashCode(), Is.EqualTo(equal.GetHashCode()));
			Assert.That(first.Equals(different), Is.False);
			Assert.That(different.Equals(first), Is.False);
		});
	}

	[Test]
	public void ScalarComparison_PreservesDefaultComparerSemantics() {
		EqualityLeafState first = new() { Score = double.NaN, Token = new EqualityToken { Value = 1 } };
		EqualityLeafState second = new() {
			Score = BitConverter.Int64BitsToDouble(0x7ff8000000000001),
			Token = new EqualityToken { Value = 11 }
		};

		AssertEquality(first, second, true);
		second.Optional = 5;
		AssertEquality(first, second, false);
	}

	[Test]
	public void NullReferencesAndOperators_PreserveReferenceBehavior() {
		EqualityBaseState first = CreateLeaf();
		EqualityBaseState sameReference = first;
		EqualityBaseState equalValue = CreateLeaf();
		EqualityBaseState? missing = null;
		NetworkObject networkFirst = first;
		NetworkObject networkEqual = equalValue;
		object objectFirst = first;
		object objectEqual = equalValue;
		EqualityComparer<EqualityBaseState> comparer = EqualityComparer<EqualityBaseState>.Default;

		Assert.Multiple(() => {
			Assert.That(first.Equals(new object()), Is.False);
			Assert.That(first.Equals(sameReference), Is.True);
			Assert.That(first.Equals(equalValue), Is.True);
			Assert.That(first == sameReference, Is.True);
			Assert.That(first == equalValue, Is.False);
			Assert.That(first != equalValue, Is.True);
			Assert.That(first == missing, Is.False);
			Assert.That(networkFirst == networkEqual, Is.False);
			Assert.That(objectFirst == objectEqual, Is.False);
			Assert.That(first!.Equals(missing), Is.False);
			Assert.That(first.Equals((object?)null), Is.False);
			Assert.That(comparer.Equals(first, null), Is.False);
			Assert.That(comparer.Equals(null, first), Is.False);
			Assert.That(comparer.Equals(null, null), Is.True);
		});
	}

	private static EqualityLeafState CreateLeaf(bool withCollections = true) {
		EqualityLeafState state = new() { DerivedValue = 2, LeafValue = "leaf" };
		state.SetBaseValue(1);
		if (withCollections) {
			state.AddBaseItem(10);
			state.SetDerivedItem(2, 20);
			state.LeafValues.Add(30);
		}
		return state;
	}

	private static EqualityContainerState CreateContainer(string memberKind, int value) {
		EqualityContainerState container = new();
		EqualityDerivedState child = new() { DerivedValue = value };
		switch (memberKind) {
			case "property": container.Child = child; break;
			case "list":
				container.Children.Add(null);
				container.Children.Add(child);
				break;
			case "dictionary":
				container.ById.Add(1, null);
				container.ById.Add(2, child);
				break;
		}
		return container;
	}

	private static void AssertEquality(EqualityBaseState first, EqualityBaseState second, bool expected) {
		Assert.Multiple(() => {
			Assert.That(first.Equals(second), Is.EqualTo(expected), "typed base equality");
			Assert.That(second.Equals(first), Is.EqualTo(expected), "reverse typed base equality");
			Assert.That(((IEquatable<EqualityBaseState>)first).Equals(second), Is.EqualTo(expected), "base IEquatable");
			Assert.That(EqualityComparer<EqualityBaseState>.Default.Equals(first, second), Is.EqualTo(expected), "base comparer");
			Assert.That(first.Equals((object)second), Is.EqualTo(expected), "object override");
			Assert.That(second.Equals((object)first), Is.EqualTo(expected), "reverse object override");
			Assert.That(object.Equals(first, second), Is.EqualTo(expected), "object equality");
			Assert.That(EqualityComparer<NetworkObject>.Default.Equals(first, second), Is.EqualTo(expected), "NetworkObject comparer");
			Assert.That(EqualityComparer<object>.Default.Equals(first, second), Is.EqualTo(expected), "object comparer");
			if (expected) {
				Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()), "equal values have equal hashes");
			}
		});
	}
}
