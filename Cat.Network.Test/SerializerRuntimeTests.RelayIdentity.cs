using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	[Test]
	public void RelayIdentity_EqualValuedEntitiesSpawnAndReplicateIndependently() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		TestEntityStorage storage = new();
		RelayServer server = new(daemon, catalogue, storage);
		RelayClient owner = new(catalogue);
		RelayClient observer = new(catalogue);
		owner.Connect(daemon.Connect());
		observer.Connect(daemon.Connect());
		Pump(server, owner, observer);

		RelayValueState first = new() { Value = 7 };
		RelayValueState second = new() { Value = 7 };
		Assert.That(first.Equals(second), Is.True);
		owner.Spawn(first);
		owner.Spawn(second);
		Pump(server, owner, observer);

		Assert.Multiple(() => {
			Assert.That(first.Id, Is.Not.EqualTo(second.Id));
			foreach (RelayValueState entity in new[] { first, second }) {
				Assert.That(storage.TryGetEntity(entity.Id, out NetworkEntity? serverEntity), Is.True);
				Assert.That(((RelayValueState)serverEntity!).Value, Is.EqualTo(7));
				Assert.That(observer.TryGetEntity(entity.Id, out NetworkEntity? observedEntity), Is.True);
				Assert.That(((RelayValueState)observedEntity!).Value, Is.EqualTo(7));
			}
		});

		first.Value = 11;
		second.Value = 13;
		Pump(server, owner, observer);

		Assert.Multiple(() => {
			foreach (RelayValueState entity in new[] { first, second }) {
				Assert.That(storage.TryGetEntity(entity.Id, out NetworkEntity? serverEntity), Is.True);
				Assert.That(((RelayValueState)serverEntity!).Value, Is.EqualTo(entity.Value));
				Assert.That(observer.TryGetEntity(entity.Id, out NetworkEntity? observedEntity), Is.True);
				Assert.That(((RelayValueState)observedEntity!).Value, Is.EqualTo(entity.Value));
			}
		});
	}

	[Test]
	public void RelayIdentity_MutationBeforeSpawnIsSentDoesNotSendAnUpdateFirst() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		RecordingRelayTransport transport = new();
		RelayClient client = new(catalogue);
		client.Connect(transport);
		RelayValueState entity = new() { Value = 1 };
		client.Spawn(entity);
		entity.Value = 2;

		client.Tick();

		Assert.That(transport.SentMessages, Is.EqualTo(new[] { BuildCreateEntityMessage(catalogue, entity.Id, entity) }));
	}

	[TestCase(false)]
	[TestCase(true)]
	public void RelayIdentity_DeletingMutatedEntityDoesNotRetainIt(bool sendBeforeDelete) {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		RecordingRelayTransport transport = new();
		RelayClient client = new(catalogue);
		client.Connect(transport);
		RelayValueState entity = new() { Value = 1 };
		client.Spawn(entity);
		if (sendBeforeDelete) {
			client.Tick();
			transport.SentMessages.Clear();
		}

		entity.Value = 2;
		client.Delete(entity);
		client.Tick();

		Assert.Multiple(() => {
			Assert.That(client.TryGetEntity(entity.Id, out _), Is.False);
			Assert.That(entity.IsSpawned, Is.False);
			Assert.That(entity.IsOwner, Is.False);
			Assert.That(transport.SentMessages, Is.EqualTo(sendBeforeDelete
				? new[] { BuildDeleteEntityMessage(entity.Id) }
				: Array.Empty<byte[]>()));
		});

		transport.SentMessages.Clear();
		RelayValueState replacement = new() { Value = 3 };
		replacement.AssignNetworkId(entity.Id);
		client.Spawn(replacement);
		client.Tick();

		// Reusing the id must not send a stale update from the deleted instance.
		Assert.That(transport.SentMessages, Is.EqualTo(new[] { BuildCreateEntityMessage(catalogue, replacement.Id, replacement) }));
	}

	[Test]
	public void RelayIdentity_EqualValuedEntitiesCanBeDeletedAfterFurtherMutation() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		RecordingRelayTransport transport = new();
		RelayClient client = new(catalogue);
		client.Connect(transport);
		RelayValueState first = new() { Value = 1 };
		RelayValueState second = new() { Value = 2 };
		client.Spawn(first);
		client.Spawn(second);
		client.Tick();
		transport.SentMessages.Clear();

		first.Value = 7;
		second.Value = 7;
		Assert.That(first.Equals(second), Is.True);
		client.Delete(first);
		client.Delete(second);
		first.Value = 9;
		second.Value = 9;
		client.Tick();

		Assert.Multiple(() => {
			Assert.That(transport.SentMessages, Is.EquivalentTo(new[] {
				BuildDeleteEntityMessage(first.Id),
				BuildDeleteEntityMessage(second.Id)
			}));
			Assert.That(client.TryGetEntity(first.Id, out _), Is.False);
			Assert.That(client.TryGetEntity(second.Id, out _), Is.False);
			Assert.That(first.IsSpawned, Is.False);
			Assert.That(second.IsSpawned, Is.False);
		});
	}

	[Test]
	public void RelayIdentity_EqualValuedServerEntitiesClearDirtyStateAfterUpdates() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		TestEntityStorage storage = new();
		RelayValueState first = new();
		RelayValueState second = new();
		storage.RegisterEntity(first);
		storage.RegisterEntity(second);
		RecordingRelayTransport firstTransport = new();
		RecordingRelayTransport secondTransport = new();
		AcceptedRelayDaemon daemon = new(
			(firstTransport, new RelayProfileState()),
			(secondTransport, new RelayProfileState()));
		RelayServer server = new(daemon, catalogue, storage);
		server.Tick();
		firstTransport.SentMessages.Clear();
		secondTransport.SentMessages.Clear();

		first.Value = 7;
		second.Value = 7;
		Assert.That(first.Equals(second), Is.True);
		server.Tick();

		Assert.Multiple(() => {
			foreach (RecordingRelayTransport transport in new[] { firstTransport, secondTransport }) {
				Assert.That(transport.SentMessages.Select(message => message[0]), Is.All.EqualTo((byte)NetworkMessageChannel.EntityMessage));
				Assert.That(transport.SentMessages.Select(message => message[1]), Is.All.EqualTo((byte)EntityMessageKind.Update));
				Assert.That(transport.SentMessages.Select(message => new Guid(message.AsSpan(2, 16))), Is.EquivalentTo(new[] { first.Id, second.Id }));
			}
			Assert.That(((INetworkObject)first).PropertyStates, Is.All.EqualTo(NetworkPropertyState.Unchanged));
			Assert.That(((INetworkObject)second).PropertyStates, Is.All.EqualTo(NetworkPropertyState.Unchanged));
		});

		firstTransport.SentMessages.Clear();
		secondTransport.SentMessages.Clear();
		server.Tick();

		Assert.Multiple(() => {
			Assert.That(firstTransport.SentMessages, Is.Empty);
			Assert.That(secondTransport.SentMessages, Is.Empty);
		});
	}

	[TestCase(false)]
	[TestCase(true)]
	public void RelayIdentity_EqualValuedProfilesClearDirtyStateAfterSend(bool profilesAlreadyKnown) {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState));
		RelayProfileState first = new();
		RelayProfileState second = new();
		RecordingRelayTransport firstTransport = new();
		RecordingRelayTransport secondTransport = new();
		AcceptedRelayDaemon daemon = new((firstTransport, first), (secondTransport, second));
		RelayServer server = new(daemon, catalogue, new TestEntityStorage());
		if (profilesAlreadyKnown) {
			server.Tick();
			firstTransport.SentMessages.Clear();
			secondTransport.SentMessages.Clear();
		}

		first.Value = 7;
		second.Value = 7;
		Assert.That(first.Equals(second), Is.True);
		server.Tick();

		ProfileMessageKind expectedKind = profilesAlreadyKnown ? ProfileMessageKind.Update : ProfileMessageKind.Create;
		Assert.Multiple(() => {
			foreach (RecordingRelayTransport transport in new[] { firstTransport, secondTransport }) {
				IEnumerable<byte[]> profileMessages = transport.SentMessages.Where(message =>
					message[0] == (byte)NetworkMessageChannel.ProfileMessage && message[1] == (byte)expectedKind);
				Assert.That(profileMessages.Select(message => new Guid(message.AsSpan(2, 16))), Is.EquivalentTo(new[] { first.Id, second.Id }));
			}
			Assert.That(first.Id, Is.Not.EqualTo(second.Id));
			Assert.That(((INetworkObject)first).PropertyStates, Is.All.EqualTo(NetworkPropertyState.Unchanged));
			Assert.That(((INetworkObject)second).PropertyStates, Is.All.EqualTo(NetworkPropertyState.Unchanged));
		});

		firstTransport.SentMessages.Clear();
		secondTransport.SentMessages.Clear();
		server.Tick();

		Assert.Multiple(() => {
			Assert.That(firstTransport.SentMessages, Is.Empty);
			Assert.That(secondTransport.SentMessages, Is.Empty);
		});
	}
}
