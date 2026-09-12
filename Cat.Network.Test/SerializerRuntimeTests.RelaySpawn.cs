using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	[Test]
	public void RelayClientSpawn_RejectsForeignClientAttachmentWithoutChangingPendingSpawns() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		RelayClient owner = new(catalogue);
		RelayClient otherClient = new(catalogue);
		RecordingRelayTransport ownerTransport = new();
		RecordingRelayTransport otherTransport = new();
		owner.Connect(ownerTransport);
		otherClient.Connect(otherTransport);
		RelayValueState entity = new() { Value = 3 };
		RelayValueState pendingEntity = new() { Value = 17 };
		owner.Spawn(entity);
		otherClient.Spawn(pendingEntity);
		Guid entityId = entity.Id;
		byte[] expectedOwnerCreate = BuildCreateEntityMessage(catalogue, entityId, entity);
		byte[] expectedOtherCreate = BuildCreateEntityMessage(catalogue, pendingEntity.Id, pendingEntity);
		NetworkPropertyState[] dirtyState = ((INetworkObject)entity).PropertyStates.ToArray();
		List<(string Kind, NetworkEntity Entity)> ownerEvents = CaptureSpawnLifecycleEvents(owner);
		List<(string Kind, NetworkEntity Entity)> otherEvents = CaptureSpawnLifecycleEvents(otherClient);

		Assert.Multiple(() => {
			Assert.That(() => otherClient.Spawn(entity), Throws.InvalidOperationException);
			Assert.That(entity.Id, Is.EqualTo(entityId));
			Assert.That(entity.Peer, Is.SameAs(owner));
			Assert.That(entity.IsOwner, Is.True);
			Assert.That(owner.TryGetEntity(entityId, out NetworkEntity? registered), Is.True);
			Assert.That(registered, Is.SameAs(entity));
			Assert.That(otherClient.TryGetEntity(entityId, out _), Is.False);
			Assert.That(otherClient.TryGetEntity(pendingEntity.Id, out NetworkEntity? pending), Is.True);
			Assert.That(pending, Is.SameAs(pendingEntity));
			Assert.That(pendingEntity.IsOwner, Is.True);
			Assert.That(((INetworkObject)entity).PropertyStates.ToArray(), Is.EqualTo(dirtyState));
			Assert.That(ownerEvents, Is.Empty);
			Assert.That(otherEvents, Is.Empty);

			owner.Tick();
			otherClient.Tick();
			Assert.That(ownerTransport.SentMessages, Is.EqualTo(new[] { expectedOwnerCreate }));
			Assert.That(otherTransport.SentMessages, Is.EqualTo(new[] { expectedOtherCreate }));
		});
	}

	[Test]
	public void RelayClientSpawn_RejectsServerAttachmentWithoutChangingState() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		TestEntityStorage storage = new();
		RelayValueState entity = new() { Value = 7 };
		storage.RegisterEntity(entity);
		RelayServer server = new(new AcceptedRelayDaemon(), catalogue, storage);
		RelayClient client = new(catalogue);
		RecordingRelayTransport transport = new();
		client.Connect(transport);
		Guid entityId = entity.Id;
		List<(string Kind, NetworkEntity Entity)> events = CaptureSpawnLifecycleEvents(client);

		Assert.Multiple(() => {
			Assert.That(() => client.Spawn(entity), Throws.InvalidOperationException);
			Assert.That(entity.Id, Is.EqualTo(entityId));
			Assert.That(entity.Peer, Is.SameAs(server));
			Assert.That(entity.IsOwner, Is.True);
			Assert.That(storage.TryGetEntity(entityId, out NetworkEntity? stored), Is.True);
			Assert.That(stored, Is.SameAs(entity));
			Assert.That(client.TryGetEntity(entityId, out _), Is.False);
			Assert.That(events, Is.Empty);
			client.Tick();
			Assert.That(transport.SentMessages, Is.Empty);
		});
	}

	[TestCase(true)]
	[TestCase(false)]
	public void RelayClientSpawn_RejectsRegisteredIdCollisionAndPreservesQueuedOperations(bool locallyOwned) {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		RelayClient client = new(catalogue);
		RecordingRelayTransport transport = new();
		client.Connect(transport);
		RelayValueState deleted = new() { Value = 11 };
		client.Spawn(deleted);
		client.Tick();
		transport.SentMessages.Clear();
		client.Delete(deleted);

		RelayValueState entity = new() { Value = 23 };
		RelayValueState pendingEntity;
		if (locallyOwned) {
			client.Spawn(entity);
			pendingEntity = entity;
		} else {
			Guid observedId = Guid.NewGuid();
			transport.Receive(BuildCreateEntityMessage(catalogue, observedId, entity));
			Assert.That(client.TryGetEntity(observedId, out NetworkEntity? observed), Is.True);
			entity = (RelayValueState)observed!;
			pendingEntity = new RelayValueState { Value = 37 };
			client.Spawn(pendingEntity);
		}

		Guid newOwnerId = Guid.NewGuid();
		client.AssignOwner(pendingEntity, newOwnerId);
		RelayValueState replacement = new() { Value = 91 };
		replacement.AssignNetworkId(entity.Id);
		Guid entityId = entity.Id;
		byte[][] expectedMessages = [
			BuildCreateEntityMessage(catalogue, pendingEntity.Id, pendingEntity),
			BuildDeleteEntityMessage(deleted.Id),
			BuildOwnershipTransferRequestMessage(pendingEntity.Id, newOwnerId)
		];
		List<(string Kind, NetworkEntity Entity)> events = CaptureSpawnLifecycleEvents(client);

		Assert.Multiple(() => {
			Assert.That(() => client.Spawn(replacement), Throws.InvalidOperationException);
			Assert.That(replacement.Id, Is.EqualTo(entityId));
			Assert.That(replacement.Peer, Is.Null);
			Assert.That(replacement.IsOwner, Is.False);
			Assert.That(entity.Id, Is.EqualTo(entityId));
			Assert.That(entity.Peer, Is.SameAs(client));
			Assert.That(entity.IsOwner, Is.EqualTo(locallyOwned));
			Assert.That(client.TryGetEntity(entityId, out NetworkEntity? registered), Is.True);
			Assert.That(registered, Is.SameAs(entity));
			Assert.That(events, Is.Empty);

			client.Tick();
			Assert.That(transport.SentMessages, Is.EqualTo(expectedMessages));
			Assert.That(deleted.Peer, Is.Null);
			Assert.That(client.TryGetEntity(deleted.Id, out _), Is.False);
			Assert.That(pendingEntity.Peer, Is.SameAs(client));
			Assert.That(pendingEntity.IsOwner, Is.False);
			Assert.That(events, Has.Count.EqualTo(2));
			Assert.That(events[0].Kind, Is.EqualTo("unobserved"));
			Assert.That(events[0].Entity, Is.SameAs(deleted));
			Assert.That(events[1].Kind, Is.EqualTo("lost"));
			Assert.That(events[1].Entity, Is.SameAs(pendingEntity));
		});
	}

	[Test]
	public void RelayClientSpawn_RejectsObservedNonownerAndStillReceivesAuthoritativeUpdates() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		TestEntityStorage storage = new();
		RelayServer server = new(daemon, catalogue, storage);
		RelayClient owner = new(catalogue);
		RelayClient observer = new(catalogue);
		owner.Connect(daemon.Connect());
		observer.Connect(daemon.Connect());
		Pump(server, owner, observer);
		Assert.That(owner.Profile, Is.Not.Null);
		Assert.That(observer.Profile, Is.Not.Null);

		RelayValueState entity = new() { Value = 3 };
		owner.Spawn(entity);
		Pump(server, owner, observer);
		Assert.That(observer.TryGetEntity(entity.Id, out NetworkEntity? observed), Is.True);
		RelayValueState remoteCopy = (RelayValueState)observed!;
		Assert.That(remoteCopy, Is.Not.SameAs(entity));
		Assert.That(remoteCopy.IsOwner, Is.False);
		List<(string Kind, NetworkEntity Entity)> events = CaptureSpawnLifecycleEvents(observer);
		Guid entityId = remoteCopy.Id;
		remoteCopy.Value = 99;
		NetworkPropertyState[] dirtyState = ((INetworkObject)remoteCopy).PropertyStates.ToArray();
		entity.Value = 17;
		owner.Tick();
		server.Tick(); // An authoritative update is waiting for the observer.

		Assert.Multiple(() => {
			Assert.That(() => observer.Spawn(remoteCopy), Throws.InvalidOperationException);
			Assert.That(remoteCopy.Id, Is.EqualTo(entityId));
			Assert.That(remoteCopy.Peer, Is.SameAs(observer));
			Assert.That(remoteCopy.IsOwner, Is.False);
			Assert.That(entity.Peer, Is.SameAs(owner));
			Assert.That(entity.IsOwner, Is.True);
			Assert.That(observer.TryGetEntity(entityId, out NetworkEntity? registered), Is.True);
			Assert.That(registered, Is.SameAs(remoteCopy));
			Assert.That(remoteCopy.Value, Is.EqualTo(99));
			Assert.That(((INetworkObject)remoteCopy).PropertyStates.ToArray(), Is.EqualTo(dirtyState));
			Assert.That(events, Is.Empty);

			Pump(server, owner, observer);
			Assert.That(remoteCopy.Value, Is.EqualTo(17));
			entity.Value = 29;
			Pump(server, owner, observer);
			Assert.That(remoteCopy.Value, Is.EqualTo(29));
			Assert.That(storage.TryGetEntity(entityId, out NetworkEntity? stored), Is.True);
			Assert.That(((RelayValueState)stored!).Value, Is.EqualTo(29));
			Assert.That(events, Is.Empty);
		});

		owner.AssignOwner(entity, observer.Profile!.Id);
		Pump(server, owner, observer);
		Assert.That(entity.IsOwner, Is.False);
		Assert.That(remoteCopy.IsOwner, Is.True);
		Assert.That(events, Has.Count.EqualTo(1));
		Assert.That(events[0].Kind, Is.EqualTo("gained"));
		Assert.That(events[0].Entity, Is.SameAs(remoteCopy));
		events.Clear();
		observer.Spawn(remoteCopy);
		remoteCopy.Value = 41;
		Pump(server, owner, observer);
		Assert.Multiple(() => {
			Assert.That(entity.Value, Is.EqualTo(41));
			Assert.That(storage.TryGetEntity(entityId, out NetworkEntity? stored), Is.True);
			Assert.That(((RelayValueState)stored!).Value, Is.EqualTo(41));
			Assert.That(events, Is.Empty);
		});
	}

	[Test]
	public void RelayClientSpawn_RejectsObservedEmptyIdBeforeAssigningId() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		RelayClient client = new(catalogue);
		RecordingRelayTransport transport = new();
		client.Connect(transport);
		transport.Receive(BuildCreateEntityMessage(catalogue, Guid.Empty, new RelayValueState { Value = 5 }));
		Assert.That(client.TryGetEntity(Guid.Empty, out NetworkEntity? observed), Is.True);
		List<(string Kind, NetworkEntity Entity)> events = CaptureSpawnLifecycleEvents(client);

		Assert.Multiple(() => {
			Assert.That(() => client.Spawn(observed!), Throws.InvalidOperationException);
			Assert.That(observed!.Id, Is.EqualTo(Guid.Empty));
			Assert.That(observed.Peer, Is.SameAs(client));
			Assert.That(observed.IsOwner, Is.False);
			Assert.That(client.TryGetEntity(Guid.Empty, out NetworkEntity? registered), Is.True);
			Assert.That(registered, Is.SameAs(observed));
			Assert.That(events, Is.Empty);
			client.Tick();
			Assert.That(transport.SentMessages, Is.Empty);
		});
	}

	[Test]
	public void RelayClientSpawn_AssignsFreshIdEvenWhenEmptyIdIsObserved() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		RelayClient client = new(catalogue);
		RecordingRelayTransport transport = new();
		client.Connect(transport);
		transport.Receive(BuildCreateEntityMessage(catalogue, Guid.Empty, new RelayValueState { Value = 5 }));
		Assert.That(client.TryGetEntity(Guid.Empty, out NetworkEntity? observed), Is.True);
		RelayValueState entity = new() { Value = 19 };

		client.Spawn(entity);

		Assert.Multiple(() => {
			Assert.That(entity.Id, Is.Not.EqualTo(Guid.Empty));
			Assert.That(entity.Peer, Is.SameAs(client));
			Assert.That(entity.IsOwner, Is.True);
			Assert.That(client.TryGetEntity(entity.Id, out NetworkEntity? registered), Is.True);
			Assert.That(registered, Is.SameAs(entity));
			Assert.That(client.TryGetEntity(Guid.Empty, out NetworkEntity? stillObserved), Is.True);
			Assert.That(stillObserved, Is.SameAs(observed));
			Assert.That(observed!.IsOwner, Is.False);
		});
		client.Tick();
		Assert.That(transport.SentMessages, Is.EqualTo(new[] { BuildCreateEntityMessage(catalogue, entity.Id, entity) }));
	}

	[Test]
	public void RelayClientSpawn_RepeatedOwnedInstancePreservesPreparedIdAndSendsUpdates() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		RelayClient client = new(catalogue);
		RelayValueState entity = new() { Value = 7 };
		Guid preparedId = Guid.NewGuid();
		entity.AssignNetworkId(preparedId);
		List<(string Kind, NetworkEntity Entity)> events = CaptureSpawnLifecycleEvents(client);
		client.Spawn(entity);
		client.Spawn(entity);
		RecordingRelayTransport transport = new();
		client.Connect(transport);
		client.Tick();
		Assert.Multiple(() => {
			Assert.That(entity.Id, Is.EqualTo(preparedId));
			Assert.That(entity.Peer, Is.SameAs(client));
			Assert.That(entity.IsOwner, Is.True);
			Assert.That(events, Has.Count.EqualTo(2));
			Assert.That(events[0].Kind, Is.EqualTo("observed"));
			Assert.That(events[0].Entity, Is.SameAs(entity));
			Assert.That(events[1].Kind, Is.EqualTo("gained"));
			Assert.That(events[1].Entity, Is.SameAs(entity));
			Assert.That(transport.SentMessages, Is.EqualTo(new[] { BuildCreateEntityMessage(catalogue, preparedId, entity) }));
		});
		events.Clear();
		transport.SentMessages.Clear();
		entity.Value = 31;
		client.Spawn(entity);
		client.Spawn(entity);
		client.Tick();
		Assert.Multiple(() => {
			Assert.That(events, Is.Empty);
			Assert.That(transport.SentMessages, Has.Count.EqualTo(1));
			Assert.That(transport.SentMessages[0][0], Is.EqualTo((byte)NetworkMessageChannel.EntityMessage));
			Assert.That(transport.SentMessages[0][1], Is.EqualTo((byte)EntityMessageKind.Update));
			Assert.That(new Guid(transport.SentMessages[0].AsSpan(2, 16)), Is.EqualTo(preparedId));
		});
	}

	[TestCase(false)]
	[TestCase(true)]
	public void RelayClientSpawn_AllowsDetachedEntityAfterDeletion(bool sentBeforeDelete) {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		RelayClient client = new(catalogue);
		RecordingRelayTransport transport = new();
		client.Connect(transport);
		RelayValueState entity = new() { Value = 13 };
		client.Spawn(entity);
		Guid entityId = entity.Id;
		if (sentBeforeDelete) {
			client.Tick();
		}

		client.Delete(entity);
		client.Tick();
		Assert.That(entity.Peer, Is.Null);
		Assert.That(client.TryGetEntity(entityId, out _), Is.False);
		transport.SentMessages.Clear();
		client.Spawn(entity);
		client.Tick();

		Assert.Multiple(() => {
			Assert.That(entity.Id, Is.EqualTo(entityId));
			Assert.That(entity.Peer, Is.SameAs(client));
			Assert.That(entity.IsOwner, Is.True);
			Assert.That(client.TryGetEntity(entityId, out NetworkEntity? registered), Is.True);
			Assert.That(registered, Is.SameAs(entity));
			Assert.That(transport.SentMessages, Is.EqualTo(new[] { BuildCreateEntityMessage(catalogue, entityId, entity) }));
		});
	}

	private static List<(string Kind, NetworkEntity Entity)> CaptureSpawnLifecycleEvents(RelayClient client) {
		List<(string Kind, NetworkEntity Entity)> events = [];
		client.EntityObserved += entity => events.Add(("observed", entity));
		client.EntityUnobserved += entity => events.Add(("unobserved", entity));
		client.EntityOwnershipGained += entity => events.Add(("gained", entity));
		client.EntityOwnershipLost += entity => events.Add(("lost", entity));
		return events;
	}
}
