using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	[Test]
	public void Deserialize_ReentrantListCallback_ForwardsAppliedOperationsInOrder(
		[Values("add", "insert", "set", "remove", "clear")] string operation,
		[Values(false, true)] bool throwAfterMutation) {
		TypeCatalogue catalogue = RegisterTypes(typeof(ValueCollectionState));
		ValueCollectionState sender = new(), intermediary = new(), observer = new(), checkpoint = new();
		sender.Values.Add(1);
		sender.Values.Add(2);
		SynchronizeCallbackPeers(sender, catalogue, intermediary, observer, checkpoint);
		int[] expected;
		switch (operation) {
			case "add": sender.Values.Add(7); expected = [1, 2, 7, 9]; break;
			case "insert": sender.Values.Insert(0, 7); expected = [1, 2]; break;
			case "set": sender.Values[0] = 7; expected = [2]; break;
			case "remove": sender.Values.RemoveAt(0); expected = [9, 2]; break;
			default: sender.Values.Clear(); expected = [9]; break;
		}

		byte[]? callbackSnapshot = null;
		bool ownerWasDirty = false;
		InvalidOperationException callbackFailure = new("List callback failed.");
		void OnChanged(NetworkList<int> list, int _) {
			if (callbackSnapshot is not null) {
				return;
			}

			ownerWasDirty = ((INetworkObject)intermediary).PropertyStates[0] == NetworkPropertyState.Modified;
			callbackSnapshot = Serialize(intermediary, catalogue, memberSelectionMode: MemberSelectionMode.Dirty);
			switch (operation) {
				case "insert":
				case "set": list.RemoveAt(0); break;
				case "remove": list.Insert(0, 9); break;
				default: list.Add(9); break;
			}
			if (throwAfterMutation) {
				throw callbackFailure;
			}
		}
		intermediary.Values.ItemAdded += OnChanged;
		intermediary.Values.ItemRemoved += OnChanged;
		intermediary.Values.IndexChanged += OnChanged;

		TestDelegate receive = () => DeserializeForForwarding(intermediary, catalogue,
			Serialize(sender, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));
		if (throwAfterMutation) {
			Assert.That(Assert.Throws<InvalidOperationException>(receive), Is.SameAs(callbackFailure));
		} else {
			Assert.DoesNotThrow(receive);
		}
		Assert.That(callbackSnapshot, Is.Not.Null, "The callback runs before deserialization returns.");
		Deserialize(checkpoint, catalogue, callbackSnapshot!);
		Deserialize(observer, catalogue, Serialize(intermediary, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));

		Assert.Multiple(() => {
			Assert.That(ownerWasDirty, Is.True, "The owner is dirty before the callback runs.");
			Assert.That(checkpoint.Values, Is.EqualTo(sender.Values), "A callback can forward the already applied operation.");
			Assert.That(intermediary.Values, Is.EqualTo(expected));
			Assert.That(observer.Values, Is.EqualTo(expected), "Incoming and callback operations retain application order.");
		});
	}

	[Test]
	public void Deserialize_ReentrantDictionaryCallback_ForwardsAppliedOperationsInOrder(
		[Values("add", "set", "set-new", "remove", "clear")] string operation,
		[Values(false, true)] bool throwAfterMutation) {
		TypeCatalogue catalogue = RegisterTypes(typeof(ValueDictionaryState));
		ValueDictionaryState sender = new(), intermediary = new(), observer = new(), checkpoint = new();
		sender.Values.Add(1, "one");
		sender.Values.Add(2, "two");
		SynchronizeCallbackPeers(sender, catalogue, intermediary, observer, checkpoint);
		KeyValuePair<int, string>[] expected;
		switch (operation) {
			case "add": sender.Values.Add(7, "seven"); expected = []; break;
			case "set": sender.Values[1] = "seven"; expected = [new(2, "two")]; break;
			case "set-new": sender.Values[7] = "seven"; expected = []; break;
			case "remove": sender.Values.Remove(1); expected = [new(1, "nine"), new(2, "two")]; break;
			default: sender.Values.Clear(); expected = [new(9, "nine")]; break;
		}

		byte[]? callbackSnapshot = null;
		bool ownerWasDirty = false;
		InvalidOperationException callbackFailure = new("Dictionary callback failed.");
		void OnChanged(NetworkDictionary<int, string> dictionary, int _) {
			if (callbackSnapshot is not null) {
				return;
			}

			ownerWasDirty = ((INetworkObject)intermediary).PropertyStates[0] == NetworkPropertyState.Modified;
			callbackSnapshot = Serialize(intermediary, catalogue, memberSelectionMode: MemberSelectionMode.Dirty);
			switch (operation) {
				case "set": dictionary.Remove(1); break;
				case "remove": dictionary.Add(1, "nine"); break;
				case "clear": dictionary.Add(9, "nine"); break;
				default: dictionary.Clear(); break;
			}
			if (throwAfterMutation) {
				throw callbackFailure;
			}
		}
		intermediary.Values.ItemAdded += OnChanged;
		intermediary.Values.ItemRemoved += OnChanged;
		intermediary.Values.ValueChanged += OnChanged;

		TestDelegate receive = () => DeserializeForForwarding(intermediary, catalogue,
			Serialize(sender, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));
		if (throwAfterMutation) {
			Assert.That(Assert.Throws<InvalidOperationException>(receive), Is.SameAs(callbackFailure));
		} else {
			Assert.DoesNotThrow(receive);
		}
		Assert.That(callbackSnapshot, Is.Not.Null, "The callback runs before deserialization returns.");
		Deserialize(checkpoint, catalogue, callbackSnapshot!);
		Deserialize(observer, catalogue, Serialize(intermediary, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));

		Assert.Multiple(() => {
			Assert.That(ownerWasDirty, Is.True, "The owner is dirty before the callback runs.");
			Assert.That(checkpoint.Values.OrderBy(pair => pair.Key), Is.EqualTo(sender.Values.OrderBy(pair => pair.Key)));
			Assert.That(intermediary.Values.OrderBy(pair => pair.Key), Is.EqualTo(expected));
			Assert.That(observer.Values.OrderBy(pair => pair.Key), Is.EqualTo(expected));
		});
	}

	[Test]
	public void Deserialize_ReentrantListAddCallback_CanRemoveJustAddedItem() {
		TypeCatalogue catalogue = RegisterTypes(typeof(ValueCollectionState));
		ValueCollectionState sender = new(), intermediary = new(), observer = new();
		intermediary.Values.ItemAdded += (list, index) => list.RemoveAt(index);
		sender.Values.Add(7);

		DeserializeForForwarding(intermediary, catalogue, Serialize(sender, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));
		Deserialize(observer, catalogue, Serialize(intermediary, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));

		Assert.Multiple(() => {
			Assert.That(intermediary.Values, Is.Empty);
			Assert.That(observer.Values, Is.Empty);
		});
	}

	[Test]
	public void Deserialize_ReentrantListCallbackException_RetainsAppliedPrefixAndStopsBatch() {
		TypeCatalogue catalogue = RegisterTypes(typeof(ValueCollectionState));
		ValueCollectionState sender = new(), intermediary = new(), observer = new();
		InvalidOperationException failure = new("Stop at first item.");
		int laterNotifications = 0;
		intermediary.Values.ItemAdded += (_, _) => throw failure;
		intermediary.Values.ItemAdded += (_, _) => laterNotifications++;
		sender.Values.Add(7);
		sender.Values.Add(11);

		Assert.That(Assert.Throws<InvalidOperationException>(() => DeserializeForForwarding(intermediary, catalogue,
			Serialize(sender, catalogue, memberSelectionMode: MemberSelectionMode.Dirty))), Is.SameAs(failure));
		Deserialize(observer, catalogue, Serialize(intermediary, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));

		Assert.Multiple(() => {
			Assert.That(intermediary.Values, Is.EqualTo(new[] { 7 }));
			Assert.That(observer.Values, Is.EqualTo(new[] { 7 }));
			Assert.That(laterNotifications, Is.Zero);
		});
	}

	[Test]
	public void Deserialize_ReentrantDictionaryCallbackException_RetainsAppliedPrefixAndStopsBatch() {
		TypeCatalogue catalogue = RegisterTypes(typeof(ValueDictionaryState));
		ValueDictionaryState sender = new(), intermediary = new(), observer = new();
		InvalidOperationException failure = new("Stop at first entry.");
		int laterNotifications = 0;
		intermediary.Values.ItemAdded += (_, _) => throw failure;
		intermediary.Values.ItemAdded += (_, _) => laterNotifications++;
		sender.Values.Add(7, "seven");
		sender.Values.Add(11, "eleven");

		Assert.That(Assert.Throws<InvalidOperationException>(() => DeserializeForForwarding(intermediary, catalogue,
			Serialize(sender, catalogue, memberSelectionMode: MemberSelectionMode.Dirty))), Is.SameAs(failure));
		Deserialize(observer, catalogue, Serialize(intermediary, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));

		Assert.Multiple(() => {
			Assert.That(intermediary.Values.Keys, Is.EqualTo(new[] { 7 }));
			Assert.That(observer.Values, Is.EqualTo(intermediary.Values));
			Assert.That(laterNotifications, Is.Zero);
		});
	}

	[Test]
	public void Deserialize_ReentrantNestedListCallback_ForwardsUpdateBeforeChangingOuterCollection(
		[Values("remove", "clear", "replace")] string mutation,
		[Values(false, true)] bool throwAfterMutation) {
		TypeCatalogue catalogue = RegisterTypes(typeof(CollectionCallbackListState), typeof(ValueCollectionState));
		CollectionCallbackListState sender = new(), intermediary = new(), observer = new();
		ValueCollectionState initialChild = new();
		initialChild.Values.Add(1);
		sender.Children.Add(initialChild);
		SynchronizeCallbackPeers(sender, catalogue, intermediary, observer);
		ValueCollectionState receivedChild = intermediary.Children[0], observedChild = observer.Children[0];
		List<string> observerEvents = [];
		observedChild.Values.ItemAdded += (_, _) => observerEvents.Add("child added");
		observer.Children.ItemRemoved += (_, _) => observerEvents.Add("outer removed");
		observer.Children.IndexChanged += (_, _) => observerEvents.Add("outer replaced");
		InvalidOperationException failure = new("Nested list callback failed.");
		receivedChild.Values.ItemAdded += (_, _) => {
			switch (mutation) {
				case "remove": intermediary.Children.RemoveAt(0); break;
				case "clear": intermediary.Children.Clear(); break;
				default:
					ValueCollectionState replacement = new();
					replacement.Values.Add(9);
					intermediary.Children[0] = replacement;
					break;
			}
			if (throwAfterMutation) {
				throw failure;
			}
		};
		sender.Children[0].Values.Add(7);

		TestDelegate receive = () => DeserializeForForwarding(intermediary, catalogue,
			Serialize(sender, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));
		if (throwAfterMutation) {
			Assert.That(Assert.Throws<InvalidOperationException>(receive), Is.SameAs(failure));
		} else {
			Assert.DoesNotThrow(receive);
		}
		Deserialize(observer, catalogue, Serialize(intermediary, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));

		Assert.Multiple(() => {
			Assert.That(observedChild.Values, Is.EqualTo(new[] { 1, 7 }), "The update targets the original child before it is detached.");
			Assert.That(observerEvents, Is.EqualTo(new[] { "child added", mutation == "replace" ? "outer replaced" : "outer removed" }));
			Assert.That(observer.Children.SelectMany(child => child.Values), Is.EqualTo(mutation == "replace" ? new[] { 9 } : []));
			Assert.That(observer.Children.Count, Is.EqualTo(intermediary.Children.Count));
			Assert.That(((INetworkObject)receivedChild).Parent, Is.Null);
		});
	}

	[Test]
	public void Deserialize_ReentrantNestedDictionaryCallback_ForwardsUpdateBeforeChangingOuterCollection(
		[Values("remove", "clear", "replace")] string mutation,
		[Values(false, true)] bool throwAfterMutation) {
		TypeCatalogue catalogue = RegisterTypes(typeof(CollectionCallbackDictionaryState), typeof(ValueCollectionState));
		CollectionCallbackDictionaryState sender = new(), intermediary = new(), observer = new();
		ValueCollectionState initialChild = new();
		initialChild.Values.Add(1);
		sender.Children.Add(3, initialChild);
		SynchronizeCallbackPeers(sender, catalogue, intermediary, observer);
		ValueCollectionState receivedChild = intermediary.Children[3], observedChild = observer.Children[3];
		List<string> observerEvents = [];
		observedChild.Values.ItemAdded += (_, _) => observerEvents.Add("child added");
		observer.Children.ItemRemoved += (_, _) => observerEvents.Add("outer removed");
		observer.Children.ValueChanged += (_, _) => observerEvents.Add("outer replaced");
		InvalidOperationException failure = new("Nested dictionary callback failed.");
		receivedChild.Values.ItemAdded += (_, _) => {
			switch (mutation) {
				case "remove": intermediary.Children.Remove(3); break;
				case "clear": intermediary.Children.Clear(); break;
				default:
					ValueCollectionState replacement = new();
					replacement.Values.Add(9);
					intermediary.Children[3] = replacement;
					break;
			}
			if (throwAfterMutation) {
				throw failure;
			}
		};
		sender.Children[3].Values.Add(7);

		TestDelegate receive = () => DeserializeForForwarding(intermediary, catalogue,
			Serialize(sender, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));
		if (throwAfterMutation) {
			Assert.That(Assert.Throws<InvalidOperationException>(receive), Is.SameAs(failure));
		} else {
			Assert.DoesNotThrow(receive);
		}
		Deserialize(observer, catalogue, Serialize(intermediary, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));

		Assert.Multiple(() => {
			Assert.That(observedChild.Values, Is.EqualTo(new[] { 1, 7 }), "The update targets the original child before it is detached.");
			Assert.That(observerEvents, Is.EqualTo(new[] { "child added", mutation == "replace" ? "outer replaced" : "outer removed" }));
			Assert.That(observer.Children.Values.SelectMany(child => child.Values), Is.EqualTo(mutation == "replace" ? new[] { 9 } : []));
			Assert.That(observer.Children.Keys, Is.EqualTo(intermediary.Children.Keys));
			Assert.That(((INetworkObject)receivedChild).Parent, Is.Null);
		});
	}

	[Test]
	public void RelayServer_ReentrantCollectionCallback_ForwardsToObserverInOrder([Values(false, true)] bool removeAddedItem) {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueCollectionState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		TestEntityStorage serverStorage = new();
		RelayServer server = new(daemon, catalogue, serverStorage);
		RelayClient ownerClient = new(catalogue), observerClient = new(catalogue);
		ownerClient.Connect(daemon.Connect());
		observerClient.Connect(daemon.Connect());
		Pump(server, ownerClient, observerClient);
		RelayValueCollectionState sender = new();
		ownerClient.Spawn(sender);
		Pump(server, ownerClient, observerClient);
		Assert.That(serverStorage.TryGetEntity(sender.Id, out NetworkEntity? serverEntity), Is.True);
		Assert.That(observerClient.TryGetEntity(sender.Id, out NetworkEntity? observerEntity), Is.True);
		RelayValueCollectionState intermediary = (RelayValueCollectionState)serverEntity!;
		RelayValueCollectionState observer = (RelayValueCollectionState)observerEntity!;
		List<int> observedAdds = [];
		observer.Values.ItemAdded += (list, index) => observedAdds.Add(list[index]);
		bool handled = false;
		intermediary.Values.ItemAdded += (list, index) => {
			if (handled) {
				return;
			}
			handled = true;
			if (removeAddedItem) {
				list.RemoveAt(index);
			} else {
				list.Add(9);
			}
		};

		sender.Values.Add(7);
		Pump(server, ownerClient, observerClient);

		Assert.Multiple(() => {
			Assert.That(intermediary.Values, Is.EqualTo(removeAddedItem ? Array.Empty<int>() : [7, 9]));
			Assert.That(observer.Values, Is.EqualTo(intermediary.Values));
			Assert.That(observedAdds, Is.EqualTo(removeAddedItem ? new[] { 7 } : [7, 9]));
		});
	}

	[Test]
	public void Deserialize_ReentrantUpdateRecording_InvalidPayloadDoesNotQueueOperation(
		[Values(false, true)] bool dictionary,
		[Values("truncated", "negative-length", "null-target", "value-target")] string invalidInput) {
		NetworkObject owner;
		INetworkCollection collection;
		if (dictionary) {
			if (invalidInput == "value-target") {
				ValueDictionaryState state = new();
				state.Values.Add(0, "zero");
				owner = state;
				collection = state.Values;
			} else {
				NullableObjectDictionaryState state = new();
				state.Children.Add(0, invalidInput == "null-target" ? null : new DirtyChildState { Value = 1 });
				owner = state;
				collection = state.Children;
			}
		} else {
			if (invalidInput == "value-target") {
				ValueCollectionState state = new();
				state.Values.Add(0);
				owner = state;
				collection = state.Values;
			} else {
				NullableObjectCollectionState state = new();
				state.Children.Add(invalidInput == "null-target" ? null : new DirtyChildState { Value = 1 });
				owner = state;
				collection = state.Children;
			}
		}
		TypeCatalogue catalogue = RegisterTypes(owner.GetType(), typeof(DirtyChildState));
		SynchronizeCallbackPeers(owner, catalogue);
		SerializationContext context = new(catalogue);
		byte[] payload = Concat(Int32(1), new[] { (byte)NetworkCollectionOperationType.Update },
			dictionary ? Concat(Int32(4), Int32(0)) : Int32(0),
			Int32(invalidInput == "negative-length" ? -1 : 4),
			invalidInput == "truncated" ? [0] : Int32(7));
		try {
			collection.Deserialize(payload, context);
		} catch (Exception exception) when (exception is ArgumentException or InvalidOperationException) {
			// This only guards operation recording; existing framing rejection rules are unchanged.
		}
		BufferWriter writer = new();
		collection.Serialize(writer, context, new SerializationOptions(MemberSelectionMode.Dirty, MemberIdentificationMode.Index));

		Assert.Multiple(() => {
			Assert.That(((INetworkObject)owner).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Unchanged));
			Assert.That(writer.GetWrittenSpan().ToArray(), Is.EqualTo(Int32(0)), "Rejected input must not become an outgoing update.");
		});
	}

	private static void SynchronizeCallbackPeers(NetworkObject sender, TypeCatalogue catalogue, params NetworkObject[] peers) {
		byte[] initial = Serialize(sender, catalogue);
		foreach (NetworkObject peer in peers) {
			Deserialize(peer, catalogue, initial);
		}
		Assert.That(catalogue.TryFindSerializer(sender.GetType(), out INetworkObjectSerializer? serializer), Is.True);
		serializer!.ClearDirtyState(sender, new SerializationContext(catalogue));
	}

	private static void DeserializeForForwarding(NetworkObject target, TypeCatalogue catalogue, byte[] payload) {
		Assert.That(catalogue.TryFindSerializer(target.GetType(), out INetworkObjectSerializer? serializer), Is.True);
		serializer!.Deserialize(target, payload, new SerializationContext(catalogue));
	}
}
