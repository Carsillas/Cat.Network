using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	[TestCase(MemberIdentificationMode.Index)]
	[TestCase(MemberIdentificationMode.Name)]
	public void ForwardBatchedListUpdates_AppliesEachChildDeltaOnce(MemberIdentificationMode identification) {
		TypeCatalogue catalogue = RegisterCollectionBatchTypes();
		BatchedCollectionEntityState source = new(), intermediary = new(), observer = new(), secondObserver = new();
		source.ListChildren.Add(new ValueCollectionState());
		source.ListChildren.Add(new ValueCollectionState());
		InitializeCollectionBatch(source, catalogue, intermediary, observer, secondObserver);
		ValueCollectionState observedChild = observer.ListChildren[0];

		foreach (int value in new[] { 1, 2 }) {
			// Equal-valued siblings must still be treated as distinct object instances.
			foreach (ValueCollectionState child in source.ListChildren) {
				child.Values.Add(value);
			}
			SendCollectionBatch(source, intermediary, catalogue, identification);
		}

		byte[] forwarded = Serialize(intermediary, catalogue, identification, MemberSelectionMode.Dirty);
		Assert.That(Serialize(intermediary, catalogue, identification, MemberSelectionMode.Dirty), Is.EqualTo(forwarded));
		Deserialize(observer, catalogue, forwarded);
		Deserialize(secondObserver, catalogue, forwarded);

		Assert.Multiple(() => {
			Assert.That(observer.ListChildren[0], Is.SameAs(observedChild));
			foreach (BatchedCollectionEntityState target in new[] { intermediary, observer, secondObserver }) {
				Assert.That(target.ListChildren.Count, Is.EqualTo(2));
				foreach (ValueCollectionState child in target.ListChildren) {
					Assert.That(child.Values, Is.EqualTo(new[] { 1, 2 }));
				}
			}
		});

		ClearCollectionBatchState(intermediary, catalogue);
		source.ListChildren[0].Values.Add(3);
		SendCollectionBatch(source, intermediary, catalogue, identification);
		Deserialize(observer, catalogue, Serialize(intermediary, catalogue, identification, MemberSelectionMode.Dirty));
		Assert.That(observer.ListChildren[0].Values, Is.EqualTo(new[] { 1, 2, 3 }));
	}

	[TestCase(MemberIdentificationMode.Index)]
	[TestCase(MemberIdentificationMode.Name)]
	public void ForwardBatchedDictionaryUpdates_AppliesEachChildDeltaOnce(MemberIdentificationMode identification) {
		TypeCatalogue catalogue = RegisterCollectionBatchTypes();
		BatchedCollectionEntityState source = new(), intermediary = new(), observer = new(), secondObserver = new();
		source.DictionaryChildren.Add(1, new ValueCollectionState());
		source.DictionaryChildren.Add(2, new ValueCollectionState());
		InitializeCollectionBatch(source, catalogue, intermediary, observer, secondObserver);
		ValueCollectionState observedChild = observer.DictionaryChildren[1];

		foreach (int value in new[] { 1, 2 }) {
			foreach (ValueCollectionState child in source.DictionaryChildren.Values) {
				child.Values.Add(value);
			}
			SendCollectionBatch(source, intermediary, catalogue, identification);
		}

		byte[] forwarded = Serialize(intermediary, catalogue, identification, MemberSelectionMode.Dirty);
		Assert.That(Serialize(intermediary, catalogue, identification, MemberSelectionMode.Dirty), Is.EqualTo(forwarded));
		Deserialize(observer, catalogue, forwarded);
		Deserialize(secondObserver, catalogue, forwarded);

		Assert.Multiple(() => {
			Assert.That(observer.DictionaryChildren[1], Is.SameAs(observedChild));
			foreach (BatchedCollectionEntityState target in new[] { intermediary, observer, secondObserver }) {
				Assert.That(target.DictionaryChildren.Keys, Is.EquivalentTo(new[] { 1, 2 }));
				foreach (ValueCollectionState child in target.DictionaryChildren.Values) {
					Assert.That(child.Values, Is.EqualTo(new[] { 1, 2 }));
				}
			}
		});

		ClearCollectionBatchState(intermediary, catalogue);
		source.DictionaryChildren[1].Values.Add(3);
		SendCollectionBatch(source, intermediary, catalogue, identification);
		Deserialize(observer, catalogue, Serialize(intermediary, catalogue, identification, MemberSelectionMode.Dirty));
		Assert.That(observer.DictionaryChildren[1].Values, Is.EqualTo(new[] { 1, 2, 3 }));
	}

	[Test]
	public void ForwardBatchedListUpdates_PreservesInsertionAndRemovalBetweenUpdates() {
		TypeCatalogue catalogue = RegisterCollectionBatchTypes();
		BatchedCollectionEntityState source = new(), intermediary = new(), observer = new();
		source.ListChildren.Add(CollectionBatchChild(10));
		source.ListChildren.Add(CollectionBatchChild(20));
		InitializeCollectionBatch(source, catalogue, intermediary, observer);
		List<string> operations = [];
		observer.ListChildren.ItemAdded += (_, index) => operations.Add($"Add:{index}");
		observer.ListChildren.ItemRemoved += (_, index) => operations.Add($"Remove:{index}");

		source.ListChildren[0].Values.Add(1);
		SendCollectionBatch(source, intermediary, catalogue);
		source.ListChildren.Insert(0, CollectionBatchChild(30));
		SendCollectionBatch(source, intermediary, catalogue);
		source.ListChildren[1].Values.Add(2);
		SendCollectionBatch(source, intermediary, catalogue);
		source.ListChildren.RemoveAt(0);
		SendCollectionBatch(source, intermediary, catalogue);
		source.ListChildren[0].Values.Add(3);
		SendCollectionBatch(source, intermediary, catalogue);

		Deserialize(observer, catalogue, Serialize(intermediary, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));

		Assert.Multiple(() => {
			Assert.That(operations, Is.EqualTo(new[] { "Add:0", "Remove:0" }));
			Assert.That(observer.ListChildren.Count, Is.EqualTo(2));
			Assert.That(observer.ListChildren[0].Values, Is.EqualTo(new[] { 10, 1, 2, 3 }));
			Assert.That(observer.ListChildren[1].Values, Is.EqualTo(new[] { 20 }));
		});
	}

	[Test]
	public void ForwardBatchedListUpdates_DoesNotRepeatAnUpdatedChildAtItsShiftedFinalIndex() {
		TypeCatalogue catalogue = RegisterCollectionBatchTypes();
		BatchedCollectionEntityState source = new(), intermediary = new(), observer = new();
		source.ListChildren.Add(new ValueCollectionState());
		InitializeCollectionBatch(source, catalogue, intermediary, observer);

		foreach (int value in new[] { 1, 2 }) {
			source.ListChildren[0].Values.Add(value);
			SendCollectionBatch(source, intermediary, catalogue);
		}
		intermediary.ListChildren.Insert(0, new ValueCollectionState());

		Deserialize(observer, catalogue, Serialize(intermediary, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));

		Assert.Multiple(() => {
			Assert.That(observer.ListChildren.Count, Is.EqualTo(2));
			Assert.That(observer.ListChildren[0].Values, Is.Empty);
			Assert.That(observer.ListChildren[1].Values, Is.EqualTo(new[] { 1, 2 }));
		});
	}

	[Test]
	public void ForwardBatchedDictionaryUpdates_PreservesKeyMovementAndReuse() {
		TypeCatalogue catalogue = RegisterCollectionBatchTypes();
		BatchedCollectionEntityState source = new(), intermediary = new(), observer = new();
		ValueCollectionState movedChild = CollectionBatchChild(10);
		source.DictionaryChildren.Add(1, movedChild);
		InitializeCollectionBatch(source, catalogue, intermediary, observer);
		List<string> operations = [];
		observer.DictionaryChildren.ItemAdded += (_, key) => operations.Add($"Add:{key}");
		observer.DictionaryChildren.ItemRemoved += (_, key) => operations.Add($"Remove:{key}");

		movedChild.Values.Add(1);
		SendCollectionBatch(source, intermediary, catalogue);
		source.DictionaryChildren.Remove(1);
		source.DictionaryChildren.Add(3, movedChild);
		SendCollectionBatch(source, intermediary, catalogue);
		source.DictionaryChildren.Add(1, CollectionBatchChild(10, 1));
		SendCollectionBatch(source, intermediary, catalogue);
		foreach (int value in new[] { 2, 3 }) {
			movedChild.Values.Add(value);
			source.DictionaryChildren[1].Values.Add(value + 2);
			SendCollectionBatch(source, intermediary, catalogue);
		}

		Deserialize(observer, catalogue, Serialize(intermediary, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));

		Assert.Multiple(() => {
			Assert.That(operations, Is.EqualTo(new[] { "Remove:1", "Add:3", "Add:1" }));
			Assert.That(observer.DictionaryChildren.Keys, Is.EquivalentTo(new[] { 1, 3 }));
			Assert.That(observer.DictionaryChildren[3].Values, Is.EqualTo(new[] { 10, 1, 2, 3 }));
			Assert.That(observer.DictionaryChildren[1].Values, Is.EqualTo(new[] { 10, 1, 4, 5 }));
		});
	}

	[TestCase(NetworkCollectionOperationType.Add)]
	[TestCase(NetworkCollectionOperationType.Insert)]
	[TestCase(NetworkCollectionOperationType.Set)]
	public void ForwardBatchedListUpdates_SkipsUpdatesCoveredByBufferedFullItem(NetworkCollectionOperationType operation) {
		TypeCatalogue catalogue = RegisterCollectionBatchTypes();
		BatchedCollectionEntityState source = new(), intermediary = new(), observer = new();
		source.ListChildren.Add(CollectionBatchChild(50));
		InitializeCollectionBatch(source, catalogue, intermediary, observer);
		ValueCollectionState child = CollectionBatchChild(10);
		int updatedIndex = operation == NetworkCollectionOperationType.Add ? 1 : 0;
		switch (operation) {
			case NetworkCollectionOperationType.Add:
				intermediary.ListChildren.Add(child);
				break;
			case NetworkCollectionOperationType.Insert:
				intermediary.ListChildren.Insert(0, child);
				break;
			case NetworkCollectionOperationType.Set:
				intermediary.ListChildren[0] = child;
				break;
		}

		// The source learns the new state before the intermediary has forwarded its pending
		// structural change to the older observer. Full serialization must not clear that batch.
		Deserialize(source, catalogue, Serialize(intermediary, catalogue));
		foreach (int value in new[] { 1, 2 }) {
			source.ListChildren[updatedIndex].Values.Add(value);
			SendCollectionBatch(source, intermediary, catalogue);
		}

		Deserialize(observer, catalogue, Serialize(intermediary, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));

		Assert.Multiple(() => {
			Assert.That(observer.ListChildren[updatedIndex].Values, Is.EqualTo(new[] { 10, 1, 2 }));
			Assert.That(observer.ListChildren.Select(item => item.Values.ToArray()),
				Is.EqualTo(source.ListChildren.Select(item => item.Values.ToArray())));
		});
	}

	[TestCase(NetworkCollectionOperationType.Add)]
	[TestCase(NetworkCollectionOperationType.Set)]
	public void ForwardBatchedDictionaryUpdates_SkipsUpdatesCoveredByBufferedFullItem(NetworkCollectionOperationType operation) {
		TypeCatalogue catalogue = RegisterCollectionBatchTypes();
		BatchedCollectionEntityState source = new(), intermediary = new(), observer = new();
		source.DictionaryChildren.Add(1, CollectionBatchChild(50));
		InitializeCollectionBatch(source, catalogue, intermediary, observer);
		ValueCollectionState child = CollectionBatchChild(10);
		int updatedKey = operation == NetworkCollectionOperationType.Add ? 2 : 1;
		if (operation == NetworkCollectionOperationType.Add) {
			intermediary.DictionaryChildren.Add(updatedKey, child);
		} else {
			intermediary.DictionaryChildren[updatedKey] = child;
		}

		Deserialize(source, catalogue, Serialize(intermediary, catalogue));
		foreach (int value in new[] { 1, 2 }) {
			source.DictionaryChildren[updatedKey].Values.Add(value);
			SendCollectionBatch(source, intermediary, catalogue);
		}

		Deserialize(observer, catalogue, Serialize(intermediary, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));

		Assert.Multiple(() => {
			Assert.That(observer.DictionaryChildren[updatedKey].Values, Is.EqualTo(new[] { 10, 1, 2 }));
			Assert.That(observer.DictionaryChildren.Keys, Is.EquivalentTo(source.DictionaryChildren.Keys));
			foreach ((int key, ValueCollectionState expected) in source.DictionaryChildren) {
				Assert.That(observer.DictionaryChildren[key].Values, Is.EqualTo(expected.Values));
			}
		});
	}

	[TestCase(false)]
	[TestCase(true)]
	public void ForwardBatchedCollectionUpdates_PreservesFullItemReplacement(bool dictionary) {
		TypeCatalogue catalogue = RegisterCollectionBatchTypes();
		BatchedCollectionEntityState source = new(), intermediary = new(), observer = new();
		source.ListChildren.Add(CollectionBatchChild(10));
		source.DictionaryChildren.Add(1, CollectionBatchChild(10));
		InitializeCollectionBatch(source, catalogue, intermediary, observer);
		ValueCollectionState originalObserverChild = dictionary ? observer.DictionaryChildren[1] : observer.ListChildren[0];
		ValueCollectionState sourceChild = dictionary ? source.DictionaryChildren[1] : source.ListChildren[0];

		foreach (int value in new[] { 1, 2 }) {
			sourceChild.Values.Add(value);
			SendCollectionBatch(source, intermediary, catalogue);
		}
		ValueCollectionState replacement = CollectionBatchChild(70);
		if (dictionary) {
			source.DictionaryChildren[1] = replacement;
		} else {
			source.ListChildren[0] = replacement;
		}
		SendCollectionBatch(source, intermediary, catalogue);
		foreach (int value in new[] { 7, 8 }) {
			replacement.Values.Add(value);
			SendCollectionBatch(source, intermediary, catalogue);
		}

		Deserialize(observer, catalogue, Serialize(intermediary, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));
		ValueCollectionState observedChild = dictionary ? observer.DictionaryChildren[1] : observer.ListChildren[0];
		Assert.Multiple(() => {
			Assert.That(observedChild, Is.Not.SameAs(originalObserverChild));
			Assert.That(observedChild.Values, Is.EqualTo(new[] { 70, 7, 8 }));
		});
	}

	[TestCase(false)]
	[TestCase(true)]
	public void ForwardBatchedCollectionUpdates_PreservesFullCollectionBetweenDeltas(bool dictionary) {
		TypeCatalogue catalogue = RegisterCollectionBatchTypes();
		BatchedCollectionEntityState source = new(), intermediary = new(), observer = new();
		source.ListChildren.Add(CollectionBatchChild(10));
		source.DictionaryChildren.Add(1, CollectionBatchChild(10));
		InitializeCollectionBatch(source, catalogue, intermediary, observer);
		ValueCollectionState sourceChild = dictionary ? source.DictionaryChildren[1] : source.ListChildren[0];

		sourceChild.Values.Add(1);
		SendCollectionBatch(source, intermediary, catalogue);
		sourceChild.Values.Clear();
		sourceChild.Values.Add(50);
		SendCollectionBatch(source, intermediary, catalogue, selection: MemberSelectionMode.All);
		foreach (int value in new[] { 51, 52 }) {
			sourceChild.Values.Add(value);
			SendCollectionBatch(source, intermediary, catalogue);
		}

		Deserialize(observer, catalogue, Serialize(intermediary, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));
		ValueCollectionState observedChild = dictionary ? observer.DictionaryChildren[1] : observer.ListChildren[0];
		Assert.Multiple(() => {
			Assert.That(observer.ListChildren.Count, Is.EqualTo(1));
			Assert.That(observer.DictionaryChildren.Count, Is.EqualTo(1));
			Assert.That(observedChild.Values, Is.EqualTo(new[] { 50, 51, 52 }));
		});
	}

	[Test]
	public void RelayServer_BatchedCollectionUpdates_ForwardEachNestedDeltaOnce() {
		TypeCatalogue catalogue = RegisterCollectionBatchTypes();
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		TestEntityStorage storage = new();
		RelayServer server = new(daemon, catalogue, storage);
		RelayClient owner = new(catalogue), observer = new(catalogue);
		owner.Connect(daemon.Connect());
		observer.Connect(daemon.Connect());
		Pump(server, owner, observer);
		BatchedCollectionEntityState entity = new();
		owner.Spawn(entity);
		Pump(server, owner, observer);
		entity.ListChildren.Add(new ValueCollectionState());
		entity.DictionaryChildren.Add(1, new ValueCollectionState());
		Pump(server, owner, observer);

		// Several ordinary client packets arrive before the server's next tick.
		foreach (int value in new[] { 1, 2 }) {
			entity.ListChildren[0].Values.Add(value);
			entity.DictionaryChildren[1].Values.Add(value);
			owner.Tick();
		}
		server.Tick();
		observer.Tick();

		Assert.That(storage.TryGetEntity(entity.Id, out NetworkEntity? storedEntity), Is.True);
		Assert.That(observer.TryGetEntity(entity.Id, out NetworkEntity? observedEntity), Is.True);
		Assert.Multiple(() => {
			foreach (BatchedCollectionEntityState target in new[] { entity, (BatchedCollectionEntityState)storedEntity!, (BatchedCollectionEntityState)observedEntity! }) {
				Assert.That(target.ListChildren[0].Values, Is.EqualTo(new[] { 1, 2 }));
				Assert.That(target.DictionaryChildren[1].Values, Is.EqualTo(new[] { 1, 2 }));
			}
		});
	}

	private static TypeCatalogue RegisterCollectionBatchTypes() {
		return RegisterTypes(typeof(BatchedCollectionEntityState), typeof(ValueCollectionState), typeof(RelayProfileState));
	}

	private static ValueCollectionState CollectionBatchChild(params int[] values) {
		ValueCollectionState child = new();
		foreach (int value in values) {
			child.Values.Add(value);
		}
		return child;
	}

	private static void InitializeCollectionBatch(BatchedCollectionEntityState source, TypeCatalogue catalogue, params BatchedCollectionEntityState[] targets) {
		byte[] baseline = Serialize(source, catalogue);
		foreach (BatchedCollectionEntityState target in targets) {
			Deserialize(target, catalogue, baseline);
		}
		ClearCollectionBatchState(source, catalogue);
	}

	private static void SendCollectionBatch(
		BatchedCollectionEntityState source,
		BatchedCollectionEntityState target,
		TypeCatalogue catalogue,
		MemberIdentificationMode identification = MemberIdentificationMode.Index,
		MemberSelectionMode selection = MemberSelectionMode.Dirty) {
		byte[] payload = Serialize(source, catalogue, identification, selection);
		Assert.That(catalogue.TryFindSerializer(target.GetType(), out INetworkObjectSerializer? serializer), Is.True);
		// Retain the intermediary's changes until the entire incoming batch is forwarded.
		serializer!.Deserialize(target, payload, new SerializationContext(catalogue));
		ClearCollectionBatchState(source, catalogue);
	}

	private static void ClearCollectionBatchState(NetworkObject target, TypeCatalogue catalogue) {
		Assert.That(catalogue.TryFindSerializer(target.GetType(), out INetworkObjectSerializer? serializer), Is.True);
		serializer!.ClearDirtyState(target, new SerializationContext(catalogue));
	}
}
