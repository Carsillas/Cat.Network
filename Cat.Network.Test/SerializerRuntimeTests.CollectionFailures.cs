using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	private static IEnumerable<TestCaseData> InvalidListSuffixes() {
		yield return new TestCaseData(Array.Empty<byte>()).SetName("CollectionFailure_List_MissingOpcode");
		yield return new TestCaseData(new byte[] { 255 }).SetName("CollectionFailure_List_UnknownOpcode");
		for (int length = 0; length < 4; length++) {
			yield return new TestCaseData(Concat(new byte[] { 0 }, new byte[length])).SetName($"CollectionFailure_List_TruncatedLength{length}");
			yield return new TestCaseData(Concat(new byte[] { 2 }, new byte[length])).SetName($"CollectionFailure_List_TruncatedIndex{length}");
		}
		yield return new TestCaseData(Concat(new byte[] { 0 }, Int32(-1))).SetName("CollectionFailure_List_NegativeLength");
		yield return new TestCaseData(Concat(new byte[] { 0 }, Int32(int.MaxValue))).SetName("CollectionFailure_List_OversizedLength");
		yield return new TestCaseData(CollectionAdd(new byte[3])).SetName("CollectionFailure_List_TruncatedItem");
		yield return new TestCaseData(CollectionInsert(-1, Int32(8))).SetName("CollectionFailure_List_NegativeInsert");
		yield return new TestCaseData(CollectionInsert(2, Int32(8))).SetName("CollectionFailure_List_InsertBeyondEnd");
		yield return new TestCaseData(CollectionRemove(-1)).SetName("CollectionFailure_List_NegativeRemove");
		yield return new TestCaseData(CollectionRemove(1)).SetName("CollectionFailure_List_RemoveBeyondEnd");
		yield return new TestCaseData(CollectionSet(1, Int32(8))).SetName("CollectionFailure_List_SetBeyondEnd");
		yield return new TestCaseData(CollectionUpdate(1, [])).SetName("CollectionFailure_List_UpdateBeyondEnd");
		yield return new TestCaseData(CollectionUpdate(0, [])).SetName("CollectionFailure_List_UpdateValueType");
	}

	private static IEnumerable<TestCaseData> InvalidDictionarySuffixes() {
		yield return new TestCaseData(Array.Empty<byte>()).SetName("CollectionFailure_Dictionary_MissingOpcode");
		yield return new TestCaseData(new byte[] { 255 }).SetName("CollectionFailure_Dictionary_UnknownOpcode");
		yield return new TestCaseData(new byte[] { 1 }).SetName("CollectionFailure_Dictionary_UnsupportedInsert");
		for (int length = 0; length < 4; length++) {
			yield return new TestCaseData(Concat(new byte[] { 0 }, new byte[length])).SetName($"CollectionFailure_Dictionary_TruncatedKeyLength{length}");
			yield return new TestCaseData(Concat(new byte[] { 0 }, Int32(4), Int32(8), new byte[length])).SetName($"CollectionFailure_Dictionary_TruncatedValueLength{length}");
		}
		yield return new TestCaseData(Concat(new byte[] { 0 }, Int32(-1))).SetName("CollectionFailure_Dictionary_NegativeKeyLength");
		yield return new TestCaseData(Concat(new byte[] { 0 }, Int32(int.MaxValue))).SetName("CollectionFailure_Dictionary_OversizedKeyLength");
		yield return new TestCaseData(CollectionDictionaryAdd(new byte[3], Utf8("eight"))).SetName("CollectionFailure_Dictionary_TruncatedKey");
		yield return new TestCaseData(Concat(new byte[] { 0 }, Int32(4), Int32(8), Int32(-1))).SetName("CollectionFailure_Dictionary_NegativeValueLength");
		yield return new TestCaseData(Concat(new byte[] { 0 }, Int32(4), Int32(8), Int32(int.MaxValue))).SetName("CollectionFailure_Dictionary_OversizedValueLength");
		yield return new TestCaseData(CollectionDictionaryAdd(Int32(7), Utf8("again"))).SetName("CollectionFailure_Dictionary_DuplicateAdd");
		yield return new TestCaseData(CollectionDictionaryUpdate(Int32(8), [])).SetName("CollectionFailure_Dictionary_UpdateMissingKey");
		yield return new TestCaseData(CollectionDictionaryUpdate(Int32(7), [])).SetName("CollectionFailure_Dictionary_UpdateValueType");
	}

	[TestCaseSource(nameof(InvalidListSuffixes))]
	public void CollectionFailure_List_RetainsAndForwardsAppliedPrefix(byte[] invalidSuffix) {
		TypeCatalogue catalogue = RegisterTypes(typeof(ValueCollectionState));
		ValueCollectionState target = new();
		int events = 0;
		target.Values.ItemAdded += (_, _) => events++;
		byte[] prefix = CollectionAdd(Int32(7));
		byte[] payload = Concat(Int32(2), prefix, invalidSuffix);

		Assert.That(() => ((INetworkCollection)target.Values).Deserialize(payload, new SerializationContext(catalogue)), Throws.TypeOf<InvalidOperationException>());
		Assert.Multiple(() => {
			Assert.That(target.Values, Is.EqualTo(new[] { 7 }));
			Assert.That(((INetworkObject)target).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Modified));
			Assert.That(events, Is.EqualTo(1));
			Assert.That(SerializeCollection(target.Values, catalogue), Is.EqualTo(CollectionPayload(prefix)));
		});
		ValueCollectionState observer = new();
		Deserialize(observer, catalogue, Serialize(target, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));
		Assert.That(observer.Values, Is.EqualTo(target.Values));
	}

	[TestCaseSource(nameof(InvalidDictionarySuffixes))]
	public void CollectionFailure_Dictionary_RetainsAndForwardsAppliedPrefix(byte[] invalidSuffix) {
		TypeCatalogue catalogue = RegisterTypes(typeof(ValueDictionaryState));
		ValueDictionaryState target = new();
		int events = 0;
		target.Values.ItemAdded += (_, _) => events++;
		byte[] prefix = CollectionDictionaryAdd(Int32(7), Utf8("seven"));
		byte[] payload = Concat(Int32(2), prefix, invalidSuffix);

		Assert.That(() => ((INetworkCollection)target.Values).Deserialize(payload, new SerializationContext(catalogue)), Throws.TypeOf<InvalidOperationException>());
		Assert.Multiple(() => {
			Assert.That(target.Values.Count, Is.EqualTo(1));
			Assert.That(target.Values[7], Is.EqualTo("seven"));
			Assert.That(((INetworkObject)target).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Modified));
			Assert.That(events, Is.EqualTo(1));
			Assert.That(SerializeCollection(target.Values, catalogue), Is.EqualTo(CollectionPayload(prefix)));
		});
		ValueDictionaryState observer = new();
		Deserialize(observer, catalogue, Serialize(target, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));
		Assert.That(observer.Values, Is.EquivalentTo(target.Values));
	}

	[TestCase(false)]
	[TestCase(true)]
	public void CollectionFailure_InvalidEnvelope_DoesNotChangeState(bool dictionary) {
		TypeCatalogue catalogue = RegisterTypes(typeof(ValueCollectionState));
		ValueCollectionState owner = new();
		INetworkCollection collection = dictionary ? new NetworkValueDictionary<int, int>() : new NetworkValueList<int>();
		collection.Initialize(owner, 0);
		foreach (byte[] payload in new[] { Array.Empty<byte>(), new byte[1], new byte[2], new byte[3], Int32(-1), Int32(int.MinValue), Int32(int.MaxValue) }) {
			Assert.That(() => collection.Deserialize(payload, new SerializationContext(catalogue)), Throws.TypeOf<InvalidOperationException>(), Convert.ToHexString(payload));
			Assert.That(((INetworkObject)owner).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Unchanged));
			Assert.That(SerializeCollection(collection, catalogue), Is.EqualTo(Int32(0)));
		}
		Assert.That(() => collection.Deserialize(Int32(0), new SerializationContext(catalogue)), Throws.Nothing);
	}

	[TestCase(false)]
	[TestCase(true)]
	public void CollectionFailure_TrailingBytes_AreReportedAfterTrackingPrefix(bool dictionary) {
		TypeCatalogue catalogue = RegisterTypes(typeof(ValueCollectionState));
		ValueCollectionState owner = new();
		INetworkCollection collection = dictionary ? new NetworkValueDictionary<int, int>() : new NetworkValueList<int>();
		collection.Initialize(owner, 0);
		byte[] operation = dictionary ? CollectionDictionaryAdd(Int32(7), Int32(7)) : CollectionAdd(Int32(7));
		byte[] payload = Concat(CollectionPayload(operation), new byte[] { 255 });
		Assert.That(() => collection.Deserialize(payload, new SerializationContext(catalogue)), Throws.TypeOf<InvalidOperationException>());
		Assert.That(SerializeCollection(collection, catalogue), Is.EqualTo(CollectionPayload(operation)));
		Assert.That(((INetworkObject)owner).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Modified));
	}

	[Test]
	public void CollectionFailure_List_PreservesEarlierAndLaterPendingOperations() {
		TypeCatalogue catalogue = RegisterTypes(typeof(ValueCollectionState));
		ValueCollectionState target = new();
		target.Values.Add(3);
		byte[] payload = CollectionPayload(CollectionAdd(Int32(7)), new byte[] { 255 });
		Assert.That(() => ((INetworkCollection)target.Values).Deserialize(payload, new SerializationContext(catalogue)), Throws.TypeOf<InvalidOperationException>());
		target.Values.Add(9);
		ValueCollectionState observer = new();
		Deserialize(observer, catalogue, Serialize(target, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));
		Assert.That(observer.Values, Is.EqualTo(new[] { 3, 7, 9 }));
	}

	[Test]
	public void CollectionFailure_Dictionary_PreservesEarlierAndLaterPendingOperations() {
		TypeCatalogue catalogue = RegisterTypes(typeof(ValueDictionaryState));
		ValueDictionaryState target = new();
		target.Values.Add(3, "three");
		byte[] payload = CollectionPayload(CollectionDictionaryAdd(Int32(7), Utf8("seven")), new byte[] { 255 });
		Assert.That(() => ((INetworkCollection)target.Values).Deserialize(payload, new SerializationContext(catalogue)), Throws.TypeOf<InvalidOperationException>());
		target.Values.Add(9, "nine");
		ValueDictionaryState observer = new();
		Deserialize(observer, catalogue, Serialize(target, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));
		Assert.That(observer.Values, Is.EquivalentTo(target.Values));
		Assert.That(observer.Values.Keys, Is.EquivalentTo(new[] { 3, 7, 9 }));
	}

	[TestCase(NetworkCollectionOperationType.Add)]
	[TestCase(NetworkCollectionOperationType.Insert)]
	[TestCase(NetworkCollectionOperationType.Set)]
	[TestCase(NetworkCollectionOperationType.Remove)]
	[TestCase(NetworkCollectionOperationType.Clear)]
	public void CollectionFailure_List_CallbackExceptionRetainsAppliedOperation(NetworkCollectionOperationType operation) {
		TypeCatalogue catalogue = RegisterTypes(typeof(ValueCollectionState));
		ValueCollectionState target = new();
		target.Values.Add(3);
		target.Values.Add(5);
		ValueCollectionState observer = new();
		Deserialize(observer, catalogue, Serialize(target, catalogue));
		ClearCollectionOwner(target, target.Values, catalogue);
		NetworkPropertyState observedState = NetworkPropertyState.Unchanged;
		void OnEvent(NetworkList<int> _, int index) {
			observedState = ((INetworkObject)target).PropertyStates[0];
			throw new ApplicationException("callback");
		}
		target.Values.ItemAdded += OnEvent;
		target.Values.ItemRemoved += OnEvent;
		target.Values.IndexChanged += OnEvent;
		byte[] incoming = operation switch {
			NetworkCollectionOperationType.Add => CollectionAdd(Int32(7)),
			NetworkCollectionOperationType.Insert => CollectionInsert(1, Int32(7)),
			NetworkCollectionOperationType.Set => CollectionSet(0, Int32(7)),
			NetworkCollectionOperationType.Remove => CollectionRemove(0),
			_ => CollectionClear()
		};
		byte[] payload = CollectionPayload(incoming, CollectionAdd(Int32(99)));
		Assert.That(() => ((INetworkCollection)target.Values).Deserialize(payload, new SerializationContext(catalogue)), Throws.TypeOf<ApplicationException>().With.Message.EqualTo("callback"));
		Assert.That(observedState, Is.EqualTo(NetworkPropertyState.Modified));
		Assert.That(SerializeCollection(target.Values, catalogue), Is.EqualTo(CollectionPayload(incoming)));
		Deserialize(observer, catalogue, Serialize(target, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));
		Assert.That(observer.Values, Is.EqualTo(target.Values));
	}

	[TestCase(NetworkCollectionOperationType.Add)]
	[TestCase(NetworkCollectionOperationType.Set)]
	[TestCase(NetworkCollectionOperationType.Remove)]
	[TestCase(NetworkCollectionOperationType.Clear)]
	public void CollectionFailure_Dictionary_CallbackExceptionRetainsAppliedOperation(NetworkCollectionOperationType operation) {
		TypeCatalogue catalogue = RegisterTypes(typeof(ValueDictionaryState));
		ValueDictionaryState target = new();
		target.Values.Add(3, "three");
		target.Values.Add(5, "five");
		ValueDictionaryState observer = new();
		Deserialize(observer, catalogue, Serialize(target, catalogue));
		ClearCollectionOwner(target, target.Values, catalogue);
		NetworkPropertyState observedState = NetworkPropertyState.Unchanged;
		void OnEvent(NetworkDictionary<int, string> _, int key) {
			observedState = ((INetworkObject)target).PropertyStates[0];
			throw new ApplicationException("callback");
		}
		target.Values.ItemAdded += OnEvent;
		target.Values.ItemRemoved += OnEvent;
		target.Values.ValueChanged += OnEvent;
		byte[] incoming = operation switch {
			NetworkCollectionOperationType.Add => CollectionDictionaryAdd(Int32(7), Utf8("seven")),
			NetworkCollectionOperationType.Set => CollectionDictionarySet(Int32(3), Utf8("THREE")),
			NetworkCollectionOperationType.Remove => CollectionDictionaryRemove(Int32(3)),
			_ => CollectionClear()
		};
		byte[] payload = CollectionPayload(incoming, CollectionDictionaryAdd(Int32(99), Utf8("later")));
		Assert.That(() => ((INetworkCollection)target.Values).Deserialize(payload, new SerializationContext(catalogue)), Throws.TypeOf<ApplicationException>().With.Message.EqualTo("callback"));
		Assert.That(observedState, Is.EqualTo(NetworkPropertyState.Modified));
		Assert.That(SerializeCollection(target.Values, catalogue), Is.EqualTo(CollectionPayload(incoming)));
		Deserialize(observer, catalogue, Serialize(target, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));
		Assert.That(observer.Values, Is.EquivalentTo(target.Values));
	}

	[TestCase(false, false)]
	[TestCase(false, true)]
	[TestCase(true, false)]
	[TestCase(true, true)]
	public void CollectionFailure_NestedUpdate_ForwardsAppliedStateAndPreservesIdentity(bool dictionary, bool pendingChange) {
		TypeCatalogue catalogue = RegisterTypes(typeof(DeepObjectCollectionLevelThreeState), typeof(DirtyChildState));
		ValueCollectionState owner = new();
		DeepObjectCollectionLevelThreeState child = new();
		child.Children.Add(new DirtyChildState { Value = 1 });
		INetworkCollection collection = CreateFailureObjectCollection(dictionary, owner, child);
		ValueCollectionState observerOwner = new();
		INetworkCollection observer = CreateFailureObjectCollection(dictionary, observerOwner);
		observer.Deserialize(SerializeCollection(collection, catalogue, MemberSelectionMode.All), new SerializationContext(catalogue));
		ClearCollectionOwner(owner, collection, catalogue);
		if (pendingChange) {
			child.Children.Add(new DirtyChildState { Value = 2 });
		}
		byte[] nested = BuildObjectPayload(BuildIndexField(0, CollectionPayload(
			CollectionAdd(ObjectItem(typeof(DirtyChildState), BuildObjectPayload(BuildIndexField(0, Int32(7))))),
			new byte[] { 255 })));
		byte[] payload = CollectionPayload(dictionary ? CollectionDictionaryUpdate(Int32(0), nested) : CollectionUpdate(0, nested));
		Assert.That(() => collection.Deserialize(payload, new SerializationContext(catalogue)), Throws.TypeOf<InvalidOperationException>());
		Assert.Multiple(() => {
			Assert.That(GetFailureChild(collection), Is.SameAs(child));
			Assert.That(((INetworkObject)child).Parent, Is.SameAs(owner));
			Assert.That(((INetworkObject)child).IsCollectionItem, Is.True);
			Assert.That(((INetworkObject)child.Children.Last()).Parent, Is.SameAs(child));
			Assert.That(((INetworkObject)owner).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Modified));
		});
		byte[] forwarded = SerializeCollection(collection, catalogue);
		Assert.That(() => observer.Deserialize(forwarded, new SerializationContext(catalogue)), Throws.Nothing);
		Assert.That(GetFailureChild(observer).Children.Select(item => item.Value), Is.EqualTo(pendingChange ? new[] { 1, 2, 7 } : new[] { 1, 7 }));
	}

	[TestCase(false)]
	[TestCase(true)]
	public void CollectionFailure_NewNestedValueIsDecodedBeforeAttachment(bool dictionary) {
		TypeCatalogue catalogue = RegisterTypes(typeof(DeepObjectCollectionLevelThreeState), typeof(DirtyChildState));
		ValueCollectionState owner = new();
		INetworkCollection collection = CreateFailureObjectCollection(dictionary, owner);
		byte[] goodValue = ObjectItem(typeof(DeepObjectCollectionLevelThreeState), BuildObjectPayload());
		byte[] badValue = ObjectItem(typeof(DeepObjectCollectionLevelThreeState), BuildObjectPayload(BuildIndexField(0,
			CollectionPayload(CollectionAdd(ObjectItem(typeof(DirtyChildState), BuildObjectPayload(BuildIndexField(0, Int32(7))))), new byte[] { 255 }))));
		byte[] payload = dictionary
			? CollectionPayload(CollectionDictionaryAdd(Int32(0), goodValue), CollectionDictionaryAdd(Int32(1), badValue))
			: CollectionPayload(CollectionAdd(goodValue), CollectionAdd(badValue));
		Assert.That(() => collection.Deserialize(payload, new SerializationContext(catalogue)), Throws.TypeOf<InvalidOperationException>());
		Assert.That(((INetworkObject)GetFailureChild(collection)).Parent, Is.SameAs(owner));
		INetworkCollection observer = CreateFailureObjectCollection(dictionary, new ValueCollectionState());
		observer.Deserialize(SerializeCollection(collection, catalogue), new SerializationContext(catalogue));
		Assert.That(GetFailureChild(observer).Children, Is.Empty);
		Assert.That(dictionary ? ((NetworkDictionary<int, DeepObjectCollectionLevelThreeState>)collection).Count : ((NetworkList<DeepObjectCollectionLevelThreeState>)collection).Count, Is.EqualTo(1));
	}

	[TestCase(false)]
	[TestCase(true)]
	public void CollectionFailure_NewNestedSetPreservesPreviousAttachmentAndPendingOperations(bool dictionary) {
		TypeCatalogue catalogue = RegisterTypes(typeof(DeepObjectCollectionLevelThreeState), typeof(DirtyChildState));
		ValueCollectionState owner = new();
		DeepObjectCollectionLevelThreeState original = new();
		original.Children.Add(new DirtyChildState { Value = 1 });
		INetworkCollection collection = CreateFailureObjectCollection(dictionary, owner, original);
		byte[] pending = SerializeCollection(collection, catalogue);
		byte[] malformed = ObjectItem(typeof(DeepObjectCollectionLevelThreeState), BuildObjectPayload(BuildIndexField(0,
			CollectionPayload(CollectionAdd(ObjectItem(typeof(DirtyChildState), BuildObjectPayload(BuildIndexField(0, Int32(7))))), new byte[] { 255 }))));
		byte[] payload = CollectionPayload(dictionary ? CollectionDictionarySet(Int32(0), malformed) : CollectionSet(0, malformed));
		Assert.That(() => collection.Deserialize(payload, new SerializationContext(catalogue)), Throws.TypeOf<InvalidOperationException>());
		Assert.Multiple(() => {
			Assert.That(GetFailureChild(collection), Is.SameAs(original));
			Assert.That(((INetworkObject)original).Parent, Is.SameAs(owner));
			Assert.That(((INetworkObject)original).PropertyIndex, Is.EqualTo(0));
			Assert.That(((INetworkObject)original).IsCollectionItem, Is.True);
			Assert.That(original.Children.Select(child => child.Value), Is.EqualTo(new[] { 1 }));
			Assert.That(SerializeCollection(collection, catalogue), Is.EqualTo(pending));
		});
	}

	[Test]
	public void CollectionFailure_NestedUpdateMarksAncestorsForForwarding() {
		TypeCatalogue catalogue = RegisterTypes(typeof(DeepObjectCollectionRootState), typeof(DeepObjectCollectionLevelTwoState), typeof(DeepObjectCollectionLevelThreeState), typeof(DirtyChildState));
		DeepObjectCollectionRootState root = new();
		DeepObjectCollectionLevelTwoState middle = new();
		DeepObjectCollectionLevelThreeState child = new();
		child.Children.Add(new DirtyChildState { Value = 1 });
		middle.Children.Add(child);
		root.Children.Add(middle);
		DeepObjectCollectionRootState observer = new();
		Deserialize(observer, catalogue, Serialize(root, catalogue));
		Assert.That(catalogue.TryFindSerializer(root.GetType(), out INetworkObjectSerializer? serializer), Is.True);
		serializer!.ClearDirtyState(root, new SerializationContext(catalogue));
		byte[] nested = BuildObjectPayload(BuildIndexField(0, CollectionPayload(
			CollectionAdd(ObjectItem(typeof(DirtyChildState), BuildObjectPayload(BuildIndexField(0, Int32(7))))), new byte[] { 255 })));
		byte[] payload = CollectionPayload(CollectionUpdate(0, nested));
		Assert.That(() => ((INetworkCollection)middle.Children).Deserialize(payload, new SerializationContext(catalogue)), Throws.TypeOf<InvalidOperationException>());
		Assert.Multiple(() => {
			Assert.That(((INetworkObject)root).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Modified));
			Assert.That(((INetworkObject)middle).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Modified));
			Assert.That(((INetworkObject)child).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Modified));
		});
		Deserialize(observer, catalogue, Serialize(root, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));
		Assert.That(observer.Children[0].Children[0].Children.Select(item => item.Value), Is.EqualTo(new[] { 1, 7 }));
	}

	[Test]
	public void CollectionFailure_Dictionary_MissingRemoveRemainsAnIdempotentOperation() {
		TypeCatalogue catalogue = RegisterTypes(typeof(ValueDictionaryState));
		ValueDictionaryState target = new();
		byte[] payload = CollectionPayload(CollectionDictionaryRemove(Int32(7)));
		Assert.That(() => ((INetworkCollection)target.Values).Deserialize(payload, new SerializationContext(catalogue)), Throws.Nothing);
		Assert.That(target.Values, Is.Empty);
		Assert.That(SerializeCollection(target.Values, catalogue), Is.EqualTo(payload));
	}

	private static INetworkCollection CreateFailureObjectCollection(bool dictionary, NetworkObject owner, DeepObjectCollectionLevelThreeState? child = null) {
		if (dictionary) {
			NetworkObjectDictionary<int, DeepObjectCollectionLevelThreeState> result = new();
			((INetworkCollection)result).Initialize(owner, 0);
			if (child is not null) {
				result.Add(0, child);
			}
			return result;
		} else {
			NetworkObjectList<DeepObjectCollectionLevelThreeState> result = new();
			((INetworkCollection)result).Initialize(owner, 0);
			if (child is not null) {
				result.Add(child);
			}
			return result;
		}
	}

	private static DeepObjectCollectionLevelThreeState GetFailureChild(INetworkCollection collection) {
		return collection is NetworkDictionary<int, DeepObjectCollectionLevelThreeState> dictionary ? dictionary[0] : ((NetworkList<DeepObjectCollectionLevelThreeState>)collection)[0];
	}

	private static byte[] SerializeCollection(INetworkCollection collection, TypeCatalogue catalogue, MemberSelectionMode selection = MemberSelectionMode.Dirty) {
		BufferWriter writer = new();
		collection.Serialize(writer, new SerializationContext(catalogue), new SerializationOptions(selection, MemberIdentificationMode.Index));
		return writer.GetWrittenSpan().ToArray();
	}

	private static void ClearCollectionOwner(NetworkObject owner, INetworkCollection collection, TypeCatalogue catalogue) {
		collection.ClearDirtyState(new SerializationContext(catalogue));
		Array.Fill(((INetworkObject)owner).PropertyStates, NetworkPropertyState.Unchanged);
	}
}
