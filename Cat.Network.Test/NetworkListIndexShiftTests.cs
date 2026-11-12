using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed class NetworkListIndexShiftTests {
	private TypeCatalogue Catalogue { get; set; } = null!;

	[SetUp]
	public void SetUp() {
		Catalogue = new TypeCatalogue();
		Catalogue.Register(typeof(ListIndexShiftState));
		Catalogue.Register(typeof(ListIndexShiftChildState));
	}

	[Test]
	public void Remove_SendsSurvivingChildUpdatesAtTheirFinalIndices(
		[Values(0, 1, 2)] int removedIndex,
		[Values] MemberIdentificationMode identificationMode) {
		ListIndexShiftState sender = CreateState(1, 2, 3);
		ListIndexShiftState observer = Synchronize(sender, identificationMode);
		ListIndexShiftChildState?[] observedChildren = observer.Children.ToArray();

		sender.Children.RemoveAt(removedIndex);
		foreach (ListIndexShiftChildState? child in sender.Children) {
			child!.Value += 90;
			child.Values.Add(7);
		}

		Apply(observer, Serialize(sender, MemberSelectionMode.Dirty, identificationMode));
		AssertEquivalent(sender, observer);
		ListIndexShiftChildState?[] survivors = observedChildren.Where((_, index) => index != removedIndex).ToArray();
		for (int index = 0; index < survivors.Length; index++) {
			Assert.That(observer.Children[index], Is.SameAs(survivors[index]));
		}
		Assert.That(((INetworkObject)observedChildren[removedIndex]!).Parent, Is.Null);
	}

	[Test]
	public void NewlySerializedChild_ShiftingItsIndexDoesNotReplayItsNestedAdd(
		[Values(NetworkCollectionOperationType.Add, NetworkCollectionOperationType.Insert, NetworkCollectionOperationType.Set)] NetworkCollectionOperationType operationType,
		[Values(false, true)] bool removeBeforeChild,
		[Values] MemberIdentificationMode identificationMode) {
		ListIndexShiftState sender = CreateState(1, 2, 3);
		ListIndexShiftState observer = Synchronize(sender, identificationMode);
		ListIndexShiftChildState child = CreateChild(7, 7);

		switch (operationType) {
			case NetworkCollectionOperationType.Add:
				sender.Children.Add(child);
				break;
			case NetworkCollectionOperationType.Insert:
				sender.Children.Insert(2, child);
				break;
			case NetworkCollectionOperationType.Set:
				sender.Children[2] = child;
				break;
		}

		if (removeBeforeChild) {
			sender.Children.RemoveAt(0);
		} else {
			sender.Children.Insert(0, CreateChild(4));
		}
		child.Value = 99;

		Apply(observer, Serialize(sender, MemberSelectionMode.Dirty, identificationMode));
		AssertEquivalent(sender, observer);
		Assert.That(observer.Children.Single(item => item?.Value == 99)!.Values, Is.EqualTo(new[] { 7 }));
	}

	[Test]
	public void EmptyList_AddThenInsertDoesNotReplayTheFirstChild(
		[Values] MemberIdentificationMode identificationMode) {
		ListIndexShiftState sender = new();
		ListIndexShiftState observer = new();
		sender.Children.Add(CreateChild(1, 7));
		sender.Children.Insert(0, CreateChild(2));

		Apply(observer, Serialize(sender, MemberSelectionMode.Dirty, identificationMode));

		AssertEquivalent(sender, observer);
		Assert.That(observer.Children[1]!.Values, Is.EqualTo(new[] { 7 }));
	}

	[Test]
	public void EqualValuedDistinctChildren_UpdatingOneAndAddingTheOtherSendsBoth(
		[Values] MemberIdentificationMode identificationMode) {
		ListIndexShiftState sender = CreateState(1);
		ListIndexShiftState observer = Synchronize(sender, identificationMode);
		ListIndexShiftChildState observedChild = observer.Children[0]!;
		sender.Children[0]!.Value = 7;
		sender.Children[0]!.Values.Add(7);
		ListIndexShiftChildState addedChild = CreateChild(7, 1, 7);
		Assert.That(sender.Children[0]!.Equals(addedChild), Is.True);
		sender.Children.Add(addedChild);

		Apply(observer, Serialize(sender, MemberSelectionMode.Dirty, identificationMode));

		AssertEquivalent(sender, observer);
		Assert.That(observer.Children[0], Is.SameAs(observedChild));
		Assert.That(observer.Children[1], Is.Not.SameAs(observedChild));
	}

	[Test]
	public void RemovedOrClearedChild_ReaddingAndShiftingItPreservesStructuralOrder(
		[Values(false, true)] bool clear,
		[Values] MemberIdentificationMode identificationMode) {
		ListIndexShiftState sender = new();
		ListIndexShiftState observer = new();
		List<string> operations = [];
		observer.Children.ItemAdded += (_, index) => operations.Add($"Add {index}");
		observer.Children.ItemRemoved += (_, index) => operations.Add($"Remove {index}");
		ListIndexShiftChildState child = CreateChild(7, 7);
		sender.Children.Add(child);
		if (clear) {
			sender.Children.Clear();
		} else {
			sender.Children.RemoveAt(0);
		}
		sender.Children.Add(child);
		sender.Children.Insert(0, CreateChild(8));

		Apply(observer, Serialize(sender, MemberSelectionMode.Dirty, identificationMode));

		AssertEquivalent(sender, observer);
		Assert.That(operations, Is.EqualTo(new[] { "Add 0", "Remove 0", "Add 0", "Add 0" }));
	}

	[Test]
	public void ReceivedUpdate_ThenLocalInsertDoesNotReplayTheShiftedChildUpdate(
		[Values] MemberIdentificationMode identificationMode) {
		ListIndexShiftState sender = CreateState(1);
		ListIndexShiftState intermediary = Synchronize(sender, identificationMode);
		ListIndexShiftState observer = Synchronize(sender, identificationMode);
		ListIndexShiftChildState observedChild = observer.Children[0]!;
		sender.Children[0]!.Values.Add(7);
		Apply(intermediary, Serialize(sender, MemberSelectionMode.Dirty, identificationMode));
		intermediary.Children.Insert(0, CreateChild(2));

		Apply(observer, Serialize(intermediary, MemberSelectionMode.Dirty, identificationMode));

		AssertEquivalent(intermediary, observer);
		Assert.That(observer.Children[1], Is.SameAs(observedChild));
		Assert.That(observedChild.Values, Is.EqualTo(new[] { 1, 7 }));
	}

	[Test]
	public void InsertRemoveAndSet_PreserveReplacementsAndSendDirtySurvivors(
		[Values] MemberIdentificationMode identificationMode) {
		ListIndexShiftState sender = CreateState(1, 2, 3);
		ListIndexShiftState observer = Synchronize(sender, identificationMode);
		ListIndexShiftChildState replacedChild = observer.Children[0]!;
		ListIndexShiftChildState survivingChild = observer.Children[1]!;
		List<string> operations = [];
		observer.Children.ItemAdded += (_, index) => operations.Add($"Add {index}");
		observer.Children.ItemRemoved += (_, index) => operations.Add($"Remove {index}");
		observer.Children.IndexChanged += (_, index) => operations.Add($"Set {index}");
		sender.Children[0] = CreateChild(4, 8);
		sender.Children.Insert(0, null);
		sender.Children.RemoveAt(3);
		sender.Children[2]!.Value = 99;
		sender.Children[2]!.Values.Add(7);

		Apply(observer, Serialize(sender, MemberSelectionMode.Dirty, identificationMode));

		AssertEquivalent(sender, observer);
		Assert.That(observer.Children[1], Is.Not.SameAs(replacedChild));
		Assert.That(((INetworkObject)replacedChild).Parent, Is.Null);
		Assert.That(observer.Children[2], Is.SameAs(survivingChild));
		Assert.That(operations, Is.EqualTo(new[] { "Set 0", "Add 0", "Remove 3" }));
	}

	[Test]
	public void NullReplacementThenRemove_SendsTheChildNowAtThatIndex(
		[Values] MemberIdentificationMode identificationMode) {
		ListIndexShiftState sender = CreateState(1, 2);
		ListIndexShiftState observer = Synchronize(sender, identificationMode);
		ListIndexShiftChildState survivingChild = observer.Children[1]!;
		sender.Children[0] = null;
		sender.Children.RemoveAt(0);
		sender.Children[0]!.Value = 99;
		sender.Children[0]!.Values.Add(7);

		Apply(observer, Serialize(sender, MemberSelectionMode.Dirty, identificationMode));

		AssertEquivalent(sender, observer);
		Assert.That(observer.Children[0], Is.SameAs(survivingChild));
	}

	private static ListIndexShiftState CreateState(params int[] values) {
		ListIndexShiftState state = new();
		foreach (int value in values) {
			state.Children.Add(CreateChild(value, value));
		}
		return state;
	}

	private static ListIndexShiftChildState CreateChild(int value, params int[] values) {
		ListIndexShiftChildState child = new() { Value = value };
		foreach (int item in values) {
			child.Values.Add(item);
		}
		return child;
	}

	private ListIndexShiftState Synchronize(ListIndexShiftState sender, MemberIdentificationMode identificationMode) {
		ListIndexShiftState observer = new();
		Apply(observer, Serialize(sender, MemberSelectionMode.All, identificationMode));
		ClearDirtyState(sender);
		ClearDirtyState(observer);
		return observer;
	}

	private byte[] Serialize(ListIndexShiftState target, MemberSelectionMode selectionMode, MemberIdentificationMode identificationMode) {
		Assert.That(Catalogue.TryFindSerializer(target.GetType(), out INetworkObjectSerializer? serializer), Is.True);
		BufferWriter writer = new();
		serializer!.Serialize(writer, target, new SerializationContext(Catalogue), new SerializationOptions(selectionMode, identificationMode));
		return writer.GetWrittenSpan().ToArray();
	}

	private void Apply(ListIndexShiftState target, byte[] payload) {
		Assert.That(Catalogue.TryFindSerializer(target.GetType(), out INetworkObjectSerializer? serializer), Is.True);
		serializer!.Deserialize(target, payload, new SerializationContext(Catalogue));
	}

	private void ClearDirtyState(ListIndexShiftState target) {
		Assert.That(Catalogue.TryFindSerializer(target.GetType(), out INetworkObjectSerializer? serializer), Is.True);
		serializer!.ClearDirtyState(target, new SerializationContext(Catalogue));
	}

	private static void AssertEquivalent(ListIndexShiftState sender, ListIndexShiftState observer) {
		Assert.That(observer.Children.Count, Is.EqualTo(sender.Children.Count));
		for (int index = 0; index < sender.Children.Count; index++) {
			ListIndexShiftChildState? expected = sender.Children[index];
			ListIndexShiftChildState? actual = observer.Children[index];
			if (expected is null) {
				Assert.That(actual, Is.Null, $"Child {index}");
				continue;
			}

			Assert.That(actual, Is.Not.Null, $"Child {index}");
			Assert.That(actual!.Value, Is.EqualTo(expected.Value), $"Child {index} value");
			Assert.That(actual.Values, Is.EqualTo(expected.Values.ToArray()), $"Child {index} nested values");
			Assert.That(((INetworkObject)actual).Parent, Is.SameAs(observer));
		}
	}
}
