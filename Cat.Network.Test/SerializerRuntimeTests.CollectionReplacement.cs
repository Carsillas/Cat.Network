using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	[Test]
	public void CollectionReplacement_DistinctObject_ReplacesReferenceAndReplicatesLaterChanges(
		[Values] bool dictionary, [Values] bool nullable, [Values] bool polymorphic) {
		DirtyChildState previous = polymorphic
			? new CollectionReplacementDerivedState { Value = 42, Detail = 1 }
			: new DirtyChildState { Value = 42 };
		DirtyChildState replacement = polymorphic
			? new CollectionReplacementDerivedState { Value = 42, Detail = 2 }
			: new DirtyChildState { Value = 42 };
		ReplacementCollection source = CreateReplacementCollection(dictionary, nullable, previous);
		ReplacementCollection receiver = CreateReplacementCollection(dictionary, nullable, new DirtyChildState());
		TypeCatalogue catalogue = RegisterTypes(source.Owner.GetType(), typeof(DirtyChildState), typeof(CollectionReplacementDerivedState));
		Deserialize(receiver.Owner, catalogue, Serialize(source.Owner, catalogue));
		DirtyChildState receivedPrevious = receiver.GetItem()!;
		ClearReplacementDirtyState(source, catalogue);

		source.SetItem(replacement);

		Assert.Multiple(() => {
			Assert.That(source.GetItem(), Is.SameAs(replacement));
			AssertReplacementAttachment(previous, null);
			AssertReplacementAttachment(replacement, source.Owner);
			Assert.That(((INetworkObject)source.Owner).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Modified));
			Assert.That(source.Events, Is.EqualTo(new[] { ("Changed", dictionary ? 7 : 0) }));
		});
		byte[] replacementDelta = Serialize(source.Owner, catalogue, memberSelectionMode: MemberSelectionMode.Dirty);
		Assert.That(replacementDelta, Is.EqualTo(BuildObjectPayload(BuildIndexField(0, CollectionPayload(
			ReplacementSet(dictionary, ObjectItem(replacement.GetType(), Serialize(replacement, catalogue))))))));
		Deserialize(receiver.Owner, catalogue, replacementDelta);
		DirtyChildState receivedReplacement = receiver.GetItem()!;
		Assert.Multiple(() => {
			Assert.That(receivedReplacement, Is.Not.SameAs(receivedPrevious));
			Assert.That(receivedReplacement.GetType(), Is.EqualTo(replacement.GetType()));
			Assert.That(receivedReplacement.Value, Is.EqualTo(42));
			AssertReplacementAttachment(receivedPrevious, null);
			AssertReplacementAttachment(receivedReplacement, receiver.Owner);
			if (polymorphic) {
				Assert.That(((CollectionReplacementDerivedState)receivedReplacement).Detail, Is.EqualTo(2));
			}
		});
		ClearReplacementDirtyState(source, catalogue);

		previous.Value = 99;

		AssertReplacementUnchanged(source, catalogue);
		replacement.Value = 73;
		if (replacement is CollectionReplacementDerivedState derived) {
			derived.Detail = 3;
		}
		byte[] updateDelta = Serialize(source.Owner, catalogue, memberSelectionMode: MemberSelectionMode.Dirty);
		byte[] childDelta = Serialize(replacement, catalogue, memberSelectionMode: MemberSelectionMode.Dirty);
		Assert.Multiple(() => {
			Assert.That(((INetworkObject)source.Owner).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Modified));
			Assert.That(updateDelta, Is.EqualTo(BuildObjectPayload(BuildIndexField(0, CollectionPayload(dictionary
				? CollectionDictionaryUpdate(Int32(7), childDelta)
				: CollectionUpdate(0, childDelta))))));
			Assert.That(source.Events, Is.EqualTo(new[] { ("Changed", dictionary ? 7 : 0) }));
		});
		Deserialize(receiver.Owner, catalogue, updateDelta);
		Assert.Multiple(() => {
			Assert.That(receiver.GetItem(), Is.SameAs(receivedReplacement));
			Assert.That(receiver.GetItem()!.Value, Is.EqualTo(73));
			if (polymorphic) {
				Assert.That(((CollectionReplacementDerivedState)receiver.GetItem()!).Detail, Is.EqualTo(3));
			}
		});
	}

	[Test]
	public void CollectionReplacement_SameInstance_IsNoOp([Values] bool dictionary, [Values] bool nullable) {
		DirtyChildState child = new() { Value = 42 };
		ReplacementCollection source = CreateReplacementCollection(dictionary, nullable, child);
		TypeCatalogue catalogue = RegisterTypes(source.Owner.GetType(), typeof(DirtyChildState));
		ClearReplacementDirtyState(source, catalogue);

		source.SetItem(child);

		Assert.Multiple(() => {
			Assert.That(source.GetItem(), Is.SameAs(child));
			AssertReplacementAttachment(child, source.Owner);
			Assert.That(source.Events, Is.Empty);
			AssertReplacementUnchanged(source, catalogue);
		});
	}

	[Test]
	public void CollectionReplacement_EqualObjectAttachedElsewhere_ThrowsWithoutChangingEitherOwner(
		[Values] bool dictionary, [Values] bool nullable) {
		DirtyChildState previous = new() { Value = 42 };
		DirtyChildState replacement = new() { Value = 42 };
		ReplacementCollection source = CreateReplacementCollection(dictionary, nullable, previous);
		ReplacementCollection foreign = CreateReplacementCollection(dictionary, nullable, replacement);
		TypeCatalogue catalogue = RegisterTypes(source.Owner.GetType(), typeof(DirtyChildState));
		ClearReplacementDirtyState(source, catalogue);
		ClearReplacementDirtyState(foreign, catalogue);

		Assert.Throws<InvalidOperationException>(() => source.SetItem(replacement));

		Assert.Multiple(() => {
			Assert.That(source.GetItem(), Is.SameAs(previous));
			Assert.That(foreign.GetItem(), Is.SameAs(replacement));
			AssertReplacementAttachment(previous, source.Owner);
			AssertReplacementAttachment(replacement, foreign.Owner);
			Assert.That(source.Events, Is.Empty);
			Assert.That(foreign.Events, Is.Empty);
			AssertReplacementUnchanged(source, catalogue);
			AssertReplacementUnchanged(foreign, catalogue);
		});
	}

	[Test]
	public void CollectionReplacement_NullTransitions_TrackReplacementAndKeepNullAssignmentANoOp([Values] bool dictionary) {
		DirtyChildState previous = new() { Value = 42 };
		ReplacementCollection source = CreateReplacementCollection(dictionary, nullable: true, previous);
		ReplacementCollection receiver = CreateReplacementCollection(dictionary, nullable: true, new DirtyChildState());
		TypeCatalogue catalogue = RegisterTypes(source.Owner.GetType(), typeof(DirtyChildState));
		Deserialize(receiver.Owner, catalogue, Serialize(source.Owner, catalogue));
		ClearReplacementDirtyState(source, catalogue);

		source.SetItem(null);

		Assert.Multiple(() => {
			Assert.That(source.GetItem(), Is.Null);
			AssertReplacementAttachment(previous, null);
			Assert.That(source.Events, Is.EqualTo(new[] { ("Changed", dictionary ? 7 : 0) }));
		});
		byte[] nullDelta = Serialize(source.Owner, catalogue, memberSelectionMode: MemberSelectionMode.Dirty);
		Assert.That(nullDelta, Is.EqualTo(BuildObjectPayload(BuildIndexField(0, CollectionPayload(ReplacementSet(dictionary, NullValue()))))));
		Deserialize(receiver.Owner, catalogue, nullDelta);
		Assert.That(receiver.GetItem(), Is.Null);
		ClearReplacementDirtyState(source, catalogue);
		source.Events.Clear();

		source.SetItem(null);

		Assert.Multiple(() => {
			Assert.That(source.Events, Is.Empty);
			AssertReplacementUnchanged(source, catalogue);
		});
		source.SetItem(previous);
		Assert.Multiple(() => {
			Assert.That(source.GetItem(), Is.SameAs(previous));
			AssertReplacementAttachment(previous, source.Owner);
			Assert.That(source.Events, Is.EqualTo(new[] { ("Changed", dictionary ? 7 : 0) }));
		});
		byte[] objectDelta = Serialize(source.Owner, catalogue, memberSelectionMode: MemberSelectionMode.Dirty);
		Assert.That(objectDelta, Is.EqualTo(BuildObjectPayload(BuildIndexField(0, CollectionPayload(
			ReplacementSet(dictionary, ObjectItem(typeof(DirtyChildState), Serialize(previous, catalogue))))))));
		Deserialize(receiver.Owner, catalogue, objectDelta);
		Assert.That(receiver.GetItem()!.Value, Is.EqualTo(42));
	}

	[Test]
	public void CollectionReplacement_EqualValueItems_KeepValueEqualityNoOp([Values] bool dictionary) {
		AssertEqualValueReplacementNoOp(dictionary, 42, 42);
		AssertEqualValueReplacementNoOp<int?>(dictionary, 42, 42);
		AssertEqualValueReplacementNoOp<int?>(dictionary, null, null);
		AssertEqualValueReplacementNoOp(dictionary, new CollectionReplacementValue { Value = 42 }, new CollectionReplacementValue { Value = 42 });
		string previous = new(['c', 'a', 't']);
		string replacement = new(['c', 'a', 't']);
		Assert.That(replacement, Is.Not.SameAs(previous));
		AssertEqualValueReplacementNoOp(dictionary, previous, replacement);
		AssertEqualValueReplacementNoOp<string?>(dictionary, null, null);
	}

	private static ReplacementCollection CreateReplacementCollection(bool dictionary, bool nullable, DirtyChildState? initial) {
		if (dictionary) {
			if (nullable) {
				NullableObjectDictionaryState owner = new();
				return TrackReplacementDictionary(owner, owner.Children, initial);
			}
			ObjectDictionaryState nonnullableOwner = new();
			return TrackReplacementDictionary(nonnullableOwner, nonnullableOwner.Children, initial!);
		}
		if (nullable) {
			NullableObjectCollectionState owner = new();
			return TrackReplacementList(owner, owner.Children, initial);
		}
		ObjectCollectionState nonnullableListOwner = new();
		return TrackReplacementList(nonnullableListOwner, nonnullableListOwner.Children, initial!);
	}

	private static ReplacementCollection TrackReplacementList<T>(NetworkObject owner, NetworkList<T> list, T initial) where T : DirtyChildState? {
		list.Add(initial);
		List<(string EventName, int Index)> events = [];
		list.ItemAdded += (_, index) => events.Add(("Added", index));
		list.ItemRemoved += (_, index) => events.Add(("Removed", index));
		list.IndexChanged += (_, index) => events.Add(("Changed", index));
		return new(owner, list, () => list[0], value => list[0] = (T)value!, events);
	}

	private static ReplacementCollection TrackReplacementDictionary<T>(NetworkObject owner, NetworkDictionary<int, T> dictionary, T initial) where T : DirtyChildState? {
		dictionary.Add(7, initial);
		List<(string EventName, int Index)> events = [];
		dictionary.ItemAdded += (_, key) => events.Add(("Added", key));
		dictionary.ItemRemoved += (_, key) => events.Add(("Removed", key));
		dictionary.ValueChanged += (_, key) => events.Add(("Changed", key));
		return new(owner, dictionary, () => dictionary[7], value => dictionary[7] = (T)value!, events);
	}

	private static void ClearReplacementDirtyState(ReplacementCollection collection, TypeCatalogue catalogue) {
		Assert.That(catalogue.TryFindSerializer(collection.Owner.GetType(), out INetworkObjectSerializer? serializer), Is.True);
		serializer!.ClearDirtyState(collection.Owner, new SerializationContext(catalogue));
	}

	private static void AssertReplacementAttachment(DirtyChildState child, NetworkObject? owner) {
		Assert.That(((INetworkObject)child).Parent, Is.SameAs(owner));
		Assert.That(((INetworkObject)child).PropertyIndex, Is.EqualTo(owner is null ? -1 : 0));
		Assert.That(((INetworkObject)child).IsCollectionItem, Is.EqualTo(owner is not null));
	}

	private static void AssertReplacementUnchanged(ReplacementCollection collection, TypeCatalogue catalogue) {
		Assert.That(((INetworkObject)collection.Owner).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Unchanged));
		Assert.That(Serialize(collection.Owner, catalogue, memberSelectionMode: MemberSelectionMode.Dirty), Is.EqualTo(BuildObjectPayload()));
		BufferWriter writer = new();
		collection.Collection.Serialize(writer, new SerializationContext(catalogue), new SerializationOptions(MemberSelectionMode.Dirty, MemberIdentificationMode.Index));
		Assert.That(writer.GetWrittenSpan().ToArray(), Is.EqualTo(CollectionPayload()));
	}

	private static byte[] ReplacementSet(bool dictionary, byte[] item) {
		return dictionary ? CollectionDictionarySet(Int32(7), item) : CollectionSet(0, item);
	}

	private static void AssertEqualValueReplacementNoOp<T>(bool dictionary, T previous, T replacement) {
		ValueCollectionState owner = new();
		INetworkCollection collection;
		Action assign;
		Func<T> read;
		int changeCount = 0;
		if (dictionary) {
			NetworkValueDictionary<int, T> values = new();
			collection = values;
			collection.Initialize(owner, 0);
			values.Add(7, previous);
			values.ValueChanged += (_, _) => changeCount++;
			assign = () => values[7] = replacement;
			read = () => values[7];
		} else {
			NetworkValueList<T> values = new();
			collection = values;
			collection.Initialize(owner, 0);
			values.Add(previous);
			values.IndexChanged += (_, _) => changeCount++;
			assign = () => values[0] = replacement;
			read = () => values[0];
		}
		SerializationContext context = new(new TypeCatalogue());
		collection.ClearDirtyState(context);
		((INetworkObject)owner).PropertyStates[0] = NetworkPropertyState.Unchanged;

		assign();

		BufferWriter writer = new();
		collection.Serialize(writer, context, new SerializationOptions(MemberSelectionMode.Dirty, MemberIdentificationMode.Index));
		Assert.Multiple(() => {
			Assert.That(changeCount, Is.Zero);
			Assert.That(((INetworkObject)owner).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Unchanged));
			Assert.That(writer.GetWrittenSpan().ToArray(), Is.EqualTo(CollectionPayload()));
			Assert.That(read(), Is.EqualTo(previous));
			if (previous is string) {
				Assert.That(read(), Is.SameAs(previous));
			}
		});
	}

	private sealed record ReplacementCollection(
		NetworkObject Owner,
		INetworkCollection Collection,
		Func<DirtyChildState?> GetItem,
		Action<DirtyChildState?> SetItem,
		List<(string EventName, int Index)> Events);
}
