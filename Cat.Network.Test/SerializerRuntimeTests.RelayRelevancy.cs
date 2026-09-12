using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	[Test]
	public void RelayServerEntityCreate_ListSnapshotStaysExactAcrossTicks() {
		RelayCollectionsState entity = new();
		entity.Values.Add(5);
		entity.Values.Add(7);

		AssertServerCollectionSnapshotStaysExact(entity, state =>
			Assert.That(state.Values, Is.EqualTo(new[] { 5, 7 })));
	}

	[Test]
	public void RelayServerEntityCreate_DictionarySnapshotStaysExactAcrossTicks() {
		RelayCollectionsState entity = new();
		entity.Labels.Add(5, "five");
		entity.Labels.Add(7, "seven");

		AssertServerCollectionSnapshotStaysExact(entity, state =>
			Assert.That(state.Labels, Is.EquivalentTo(new Dictionary<int, string> { [5] = "five", [7] = "seven" })));
	}

	[TestCase(true)]
	[TestCase(false)]
	public void RelayServerEntityCreate_PreservesPendingDeltaForKnownObservers(bool newObserverFirst) {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayCollectionsState));
		int profileValue = 0;
		MemoryRelayDaemon daemon = new(() => new RelayProfileState { Value = ++profileValue });
		RelevantEntityStorage storage = new();
		RelayServer server = new(daemon, catalogue, storage);
		RelayClient owner = new(catalogue);
		RelayClient knownObserver = new(catalogue);
		RelayClient newObserver = new(catalogue);
		RelayClient[] connectionOrder = newObserverFirst
			? [newObserver, owner, knownObserver]
			: [owner, knownObserver, newObserver];
		foreach (RelayClient client in connectionOrder) {
			client.Connect(daemon.Connect());
			Pump(server, client);
		}

		RelayCollectionsState entity = new();
		storage.RegisterEntity(entity);
		storage.Allow(owner.Profile!.Id, entity.Id);
		storage.Allow(knownObserver.Profile!.Id, entity.Id);
		Pump(server, owner, knownObserver, newObserver);
		Assert.That(owner.TryGetEntity(entity.Id, out NetworkEntity? ownedEntity), Is.True);
		Assert.That(ownedEntity!.IsOwner, Is.True);
		Assert.That(knownObserver.TryGetEntity(entity.Id, out NetworkEntity? knownEntity), Is.True);
		Assert.That(knownEntity!.IsOwner, Is.False);
		Assert.That(newObserver.TryGetEntity(entity.Id, out _), Is.False);

		RelayCollectionsState ownedCollections = (RelayCollectionsState)ownedEntity;
		ownedCollections.Values.Add(5);
		ownedCollections.Labels.Add(5, "five");
		owner.Tick();
		storage.Allow(newObserver.Profile!.Id, entity.Id);

		Pump(server, owner, knownObserver, newObserver);

		Assert.That(newObserver.TryGetEntity(entity.Id, out NetworkEntity? newEntity), Is.True);
		foreach (RelayCollectionsState state in new[] { entity, ownedCollections, (RelayCollectionsState)knownEntity, (RelayCollectionsState)newEntity! }) {
			Assert.Multiple(() => {
				Assert.That(state.Values, Is.EqualTo(new[] { 5 }));
				Assert.That(state.Labels, Is.EquivalentTo(new Dictionary<int, string> { [5] = "five" }));
			});
		}
	}

	[Test]
	public void RelayServerEntityCreate_ClearsNestedDirtyStateOnlyAfterSuccessfulSnapshot() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayObjectCollectionState), typeof(DirtyChildState));
		RecordingRelayTransport transport = new();
		RelayProfileState profile = new();
		AcceptedRelayDaemon daemon = new((transport, profile));
		RelevantEntityStorage storage = new();
		RelayServer server = new(daemon, catalogue, storage);
		RelayObjectCollectionState entity = new();
		DirtyChildState child = new() { Value = 5 };
		entity.Children.Add(child);
		storage.RegisterEntity(entity);
		byte[] fullState = Serialize(entity, catalogue);

		server.Tick();
		Assert.That(((INetworkObject)entity).PropertyStates, Does.Contain(NetworkPropertyState.Modified));
		Assert.That(((INetworkObject)child).PropertyStates, Does.Contain(NetworkPropertyState.Replaced));

		storage.Allow(profile.Id, entity.Id);
		server.Tick();

		BufferWriter dirtyCollection = new();
		((INetworkCollection)entity.Children).Serialize(dirtyCollection, new SerializationContext(catalogue),
			new SerializationOptions(MemberSelectionMode.Dirty, MemberIdentificationMode.Index));
		Assert.Multiple(() => {
			Assert.That(transport.SentMessages.Count(message => IsEntityMessage(message, EntityMessageKind.Create)), Is.EqualTo(1));
			Assert.That(((INetworkObject)entity).PropertyStates, Is.All.EqualTo(NetworkPropertyState.Unchanged));
			Assert.That(((INetworkObject)child).PropertyStates, Is.All.EqualTo(NetworkPropertyState.Unchanged));
			Assert.That(dirtyCollection.GetWrittenSpan().ToArray(), Is.EqualTo(Int32(0)));
			Assert.That(Serialize(entity, catalogue), Is.EqualTo(fullState));
		});
	}

	[Test]
	public void RelayServerEntityCreate_FailedSendRetainsDirtyState() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayCollectionsState));
		ThrowingRelayTransport transport = new();
		AcceptedRelayDaemon daemon = new((transport, new RelayProfileState()));
		TestEntityStorage storage = new();
		RelayServer server = new(daemon, catalogue, storage);
		server.Tick();
		transport.SentMessages.Clear();
		transport.ThrowOnSend = true;
		RelayCollectionsState entity = new();
		entity.Values.Add(5);
		entity.Labels.Add(5, "five");
		storage.RegisterEntity(entity);
		byte[] dirtyState = Serialize(entity, catalogue, memberSelectionMode: MemberSelectionMode.Dirty);

		server.Tick();
		server.Tick();

		Assert.Multiple(() => {
			Assert.That(transport.SentMessages, Is.Empty);
			Assert.That(((INetworkObject)entity).PropertyStates, Does.Contain(NetworkPropertyState.Modified));
			Assert.That(Serialize(entity, catalogue, memberSelectionMode: MemberSelectionMode.Dirty), Is.EqualTo(dirtyState));
		});
	}

	[TestCase(true)]
	[TestCase(false)]
	public void RelayServerEntityCreate_FailedObserverDoesNotReplaySuccessfulSnapshot(bool failedObserverFirst) {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayCollectionsState));
		ThrowingRelayTransport failedTransport = new();
		RecordingRelayTransport successfulTransport = new();
		(IRelayTransport Transport, NetworkProfile Profile) failedConnection = (failedTransport, new RelayProfileState { Value = 1 });
		(IRelayTransport Transport, NetworkProfile Profile) successfulConnection = (successfulTransport, new RelayProfileState { Value = 2 });
		AcceptedRelayDaemon daemon = failedObserverFirst
			? new(failedConnection, successfulConnection)
			: new(successfulConnection, failedConnection);
		TestEntityStorage storage = new();
		RelayServer server = new(daemon, catalogue, storage);
		server.Tick();
		failedTransport.ThrowOnSend = true;
		RelayCollectionsState entity = new();
		entity.Values.Add(5);
		entity.Labels.Add(5, "five");
		storage.RegisterEntity(entity);

		server.Tick();
		server.Tick();

		Assert.Multiple(() => {
			Assert.That(successfulTransport.SentMessages.Count(message => IsEntityMessage(message, EntityMessageKind.Create)), Is.EqualTo(1));
			Assert.That(successfulTransport.SentMessages.Any(message => IsEntityMessage(message, EntityMessageKind.Update)), Is.False);
			Assert.That(((INetworkObject)entity).PropertyStates, Is.All.EqualTo(NetworkPropertyState.Unchanged));
		});
	}

	private static void AssertServerCollectionSnapshotStaysExact(RelayCollectionsState entity, Action<RelayCollectionsState> assertState) {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayCollectionsState));
		int profileValue = 0;
		MemoryRelayDaemon daemon = new(() => new RelayProfileState { Value = ++profileValue });
		TestEntityStorage storage = new();
		storage.RegisterEntity(entity);
		RelayServer server = new(daemon, catalogue, storage);
		RelayClient firstClient = new(catalogue);
		RelayClient secondClient = new(catalogue);
		firstClient.Connect(daemon.Connect());
		secondClient.Connect(daemon.Connect());

		Pump(server, firstClient, secondClient);
		Assert.That(firstClient.TryGetEntity(entity.Id, out NetworkEntity? firstEntity), Is.True);
		Assert.That(secondClient.TryGetEntity(entity.Id, out NetworkEntity? secondEntity), Is.True);
		for (int tick = 0; tick < 3; tick++) {
			server.Tick();
			firstClient.Tick();
			secondClient.Tick();
			Assert.Multiple(() => {
				assertState(entity);
				assertState((RelayCollectionsState)firstEntity!);
				assertState((RelayCollectionsState)secondEntity!);
			});
		}
	}

	private static bool IsEntityMessage(byte[] message, EntityMessageKind kind) {
		return message.Length >= 2 && message[0] == (byte)NetworkMessageChannel.EntityMessage && message[1] == (byte)kind;
	}
}
