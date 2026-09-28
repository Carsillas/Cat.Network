using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed class MemoryRelayHandshakeTests {
	[TestCase(false)]
	[TestCase(true)]
	public void ConnectAndSpawnImmediately_ReplicatesToServerAndObserver(bool clientTicksFirst) {
		TypeCatalogue catalogue = CreateCatalogue();
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		TestEntityStorage storage = new();
		RecordingServer server = new(daemon, catalogue, storage);
		RelayClient owner = new(catalogue);
		RelayClient observer = new(catalogue);
		int ownerObserved = 0;
		int observerObserved = 0;
		owner.EntityObserved += _ => ownerObserved++;
		observer.EntityObserved += _ => observerObserved++;
		observer.Connect(daemon.Connect());
		owner.Connect(daemon.Connect());
		RelayValueState entity = new() { Value = 7 };

		owner.Spawn(entity);
		Pump(server, clientTicksFirst, owner, observer);

		Assert.That(owner.Profile, Is.Not.Null);
		Assert.That(observer.Profile, Is.Not.Null);
		AssertReplicated(storage, owner, observer, entity, 7);
		Assert.That(server.Messages, Is.EqualTo(new[] { (EntityMessageKind.Create, entity.Id, (int?)7) }));

		entity.Value = 11;
		Pump(server, clientTicksFirst, owner, observer);

		AssertReplicated(storage, owner, observer, entity, 11);
		Assert.Multiple(() => {
			Assert.That(server.Messages, Is.EqualTo(new[] {
				(EntityMessageKind.Create, entity.Id, (int?)7),
				(EntityMessageKind.Update, entity.Id, (int?)11)
			}));
			Assert.That(ownerObserved, Is.EqualTo(1));
			Assert.That(observerObserved, Is.EqualTo(1));
		});
	}

	[Test]
	public void PendingCreatesAndUpdates_ArriveInOrderAcrossBothSidesOfPong() {
		TypeCatalogue catalogue = CreateCatalogue();
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		TestEntityStorage storage = new();
		RecordingServer server = new(daemon, catalogue, storage);
		RelayClient owner = new(catalogue);
		RelayClient observer = new(catalogue);
		observer.Connect(daemon.Connect());
		Pump(server, false, observer);
		owner.Connect(daemon.Connect());
		RelayValueState first = new() { Value = 7 };
		RelayValueState second = new() { Value = 20 };

		// Both packets precede the first handshake ping and are drained while still pending.
		owner.Spawn(first);
		owner.Tick();
		first.Value = 8;
		owner.Tick();
		server.Tick();

		Assert.Multiple(() => {
			Assert.That(owner.Profile, Is.Null);
			Assert.That(storage.TryGetEntity(first.Id, out _), Is.False);
			Assert.That(server.Messages, Is.Empty);
		});

		// This tick sends Pong, an update, and another create, then a later tick adds an update.
		first.Value = 9;
		owner.Spawn(second);
		owner.Tick();
		second.Value = 21;
		owner.Tick();
		server.Tick();
		Pump(server, false, owner, observer);

		AssertReplicated(storage, owner, observer, first, 9);
		AssertReplicated(storage, owner, observer, second, 21);
		Assert.That(server.Messages, Is.EqualTo(new[] {
			(EntityMessageKind.Create, first.Id, (int?)7),
			(EntityMessageKind.Update, first.Id, (int?)8),
			(EntityMessageKind.Update, first.Id, (int?)9),
			(EntityMessageKind.Create, second.Id, (int?)20),
			(EntityMessageKind.Update, second.Id, (int?)21)
		}));
	}

	[Test]
	public void AcceptedTransport_RetainsPendingPacketsBeforeNewArrivalsWithoutFollowingRemote() {
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		MemoryRelayTransport clientTransport = daemon.Connect();
		MemoryRelayTransport serverTransport = clientTransport.Remote!;
		RelayClient client = new(CreateCatalogue());
		client.Connect(clientTransport);
		byte[] first = [(byte)NetworkMessageChannel.Application, 7];
		byte[] second = [(byte)NetworkMessageChannel.Application, 8];
		byte[] third = [(byte)NetworkMessageChannel.Application, 9];

		clientTransport.Send(first);
		daemon.Tick();
		Assert.That(daemon.TryAcceptConnection(out _, out _), Is.False);
		clientTransport.Send(second);
		client.Tick();
		clientTransport.Send(third);

		// Queued input belongs to the receiving transport even if public routing links change.
		clientTransport.Remote = new MemoryRelayTransport();
		serverTransport.Remote = new MemoryRelayTransport();
		daemon.Tick();
		Assert.That(daemon.TryAcceptConnection(out IRelayTransport? accepted, out _), Is.True);
		Assert.That(accepted, Is.SameAs(serverTransport));
		List<byte[]> received = [];
		accepted!.MessageReceived += (sender, message) => {
			Assert.That(sender, Is.SameAs(serverTransport));
			received.Add(message.ToArray());
		};
		MemoryRelayTransport laterSender = new() { Remote = serverTransport };
		byte[] fourth = [(byte)NetworkMessageChannel.Application, 10];
		laterSender.Send(fourth);

		Assert.That(received, Is.Empty);
		accepted.PumpMessages();
		accepted.PumpMessages();
		daemon.Tick();

		Assert.Multiple(() => {
			Assert.That(received, Is.EqualTo(new[] { first, second, third, fourth }));
			Assert.That(daemon.TryAcceptConnection(out _, out _), Is.False);
		});
	}

	[Test]
	public void HandshakeOnly_AcceptsOnceWithoutReplayingControlPackets() {
		RelayProfileState profile = new();
		int profileCount = 0;
		MemoryRelayDaemon daemon = new(() => {
			profileCount++;
			return profile;
		});
		MemoryRelayTransport clientTransport = daemon.Connect();
		RelayClient client = new(CreateCatalogue());
		client.Connect(clientTransport);
		List<byte[]> clientMessages = [];
		clientTransport.MessageReceived += (_, message) => clientMessages.Add(message.ToArray());

		daemon.Tick();
		daemon.Tick();
		Assert.That(daemon.TryAcceptConnection(out _, out _), Is.False);
		client.Tick();
		clientTransport.Send([0]); // Pending handshake control is not application input.
		clientTransport.Send([1]); // An extra pong must not create a second connection.
		daemon.Tick();

		Assert.That(daemon.TryAcceptConnection(out IRelayTransport? accepted, out NetworkProfile? acceptedProfile), Is.True);
		List<byte[]> received = [];
		accepted!.MessageReceived += (_, message) => received.Add(message.ToArray());
		accepted.PumpMessages();
		daemon.Tick();
		client.Tick();
		accepted.PumpMessages();

		Assert.Multiple(() => {
			Assert.That(accepted, Is.SameAs(clientTransport.Remote));
			Assert.That(acceptedProfile, Is.SameAs(profile));
			Assert.That(profileCount, Is.EqualTo(1));
			Assert.That(clientMessages, Is.EqualTo(new[] { new byte[] { 0 } }));
			Assert.That(received, Is.Empty);
			Assert.That(daemon.TryAcceptConnection(out _, out _), Is.False);
		});
	}

	[Test]
	public void MultiplePendingConnections_RetainTheirOwnPacketsUntilEachResponds() {
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		MemoryRelayTransport firstTransport = daemon.Connect();
		MemoryRelayTransport secondTransport = daemon.Connect();
		RelayClient firstClient = new(CreateCatalogue());
		RelayClient secondClient = new(CreateCatalogue());
		firstClient.Connect(firstTransport);
		secondClient.Connect(secondTransport);
		byte[] firstPacket = [(byte)NetworkMessageChannel.Application, 10];
		byte[] secondPacket = [(byte)NetworkMessageChannel.Application, 20];
		byte[] laterPacket = [(byte)NetworkMessageChannel.Application, 21];
		firstTransport.Send(firstPacket);
		secondTransport.Send(secondPacket);
		daemon.Tick();

		firstClient.Tick();
		daemon.Tick();
		Assert.That(daemon.TryAcceptConnection(out IRelayTransport? firstAccepted, out _), Is.True);
		Assert.That(firstAccepted, Is.SameAs(firstTransport.Remote));
		Assert.That(daemon.TryAcceptConnection(out _, out _), Is.False);
		List<byte[]> firstReceived = [];
		firstAccepted!.MessageReceived += (_, message) => firstReceived.Add(message.ToArray());
		firstAccepted.PumpMessages();

		secondTransport.Send(laterPacket);
		daemon.Tick();
		Assert.That(daemon.TryAcceptConnection(out _, out _), Is.False);
		secondClient.Tick();
		daemon.Tick();
		Assert.That(daemon.TryAcceptConnection(out IRelayTransport? secondAccepted, out _), Is.True);
		Assert.That(secondAccepted, Is.SameAs(secondTransport.Remote));
		List<byte[]> secondReceived = [];
		secondAccepted!.MessageReceived += (_, message) => secondReceived.Add(message.ToArray());
		secondAccepted.PumpMessages();
		firstAccepted.PumpMessages();
		secondAccepted.PumpMessages();
		daemon.Tick();

		Assert.Multiple(() => {
			Assert.That(firstReceived, Is.EqualTo(new[] { firstPacket }));
			Assert.That(secondReceived, Is.EqualTo(new[] { secondPacket, laterPacket }));
			Assert.That(daemon.TryAcceptConnection(out _, out _), Is.False);
		});
	}

	[Test]
	public void PlainMemoryTransport_CopiesAndDrainsMessagesInOrderIncludingCallbackSends() {
		MemoryRelayTransport receiver = new();
		MemoryRelayTransport sender = new() { Remote = receiver };
		List<byte[]> received = [];
		receiver.MessageReceived += (transport, message) => {
			Assert.That(transport, Is.SameAs(receiver));
			received.Add(message.ToArray());
			if (received.Count == 1) {
				sender.Send([5, 6]);
			}
		};
		byte[] packet = [1, 2];
		sender.Send(packet);
		packet[0] = 3;
		packet[1] = 4;
		sender.Send(packet);

		Assert.That(received, Is.Empty);
		receiver.PumpMessages();
		Assert.That(received, Has.Count.EqualTo(3));
		receiver.PumpMessages();

		Assert.That(received, Is.EqualTo(new[] { new byte[] { 1, 2 }, new byte[] { 3, 4 }, new byte[] { 5, 6 } }));
	}

	private static TypeCatalogue CreateCatalogue() {
		TypeCatalogue catalogue = new();
		catalogue.Register(typeof(RelayProfileState));
		catalogue.Register(typeof(RelayValueState));
		return catalogue;
	}

	private static void Pump(RelayServer server, bool clientTicksFirst, params RelayClient[] clients) {
		for (int i = 0; i < 4; i++) {
			if (!clientTicksFirst) {
				server.Tick();
			}

			foreach (RelayClient client in clients) {
				client.Tick();
			}

			if (clientTicksFirst) {
				server.Tick();
			}
		}
	}

	private static void AssertReplicated(TestEntityStorage storage, RelayClient owner, RelayClient observer, RelayValueState entity, int value) {
		Assert.That(storage.TryGetEntity(entity.Id, out NetworkEntity? stored), Is.True);
		Assert.That(observer.TryGetEntity(entity.Id, out NetworkEntity? observed), Is.True);
		Assert.Multiple(() => {
			Assert.That(((RelayValueState)stored!).Value, Is.EqualTo(value));
			Assert.That(((RelayValueState)observed!).Value, Is.EqualTo(value));
			Assert.That(entity.Value, Is.EqualTo(value));
			Assert.That(owner.TryGetEntity(entity.Id, out NetworkEntity? owned), Is.True);
			Assert.That(owned, Is.SameAs(entity));
			Assert.That(entity.IsSpawned, Is.True);
			Assert.That(entity.IsOwner, Is.True);
			Assert.That(observed!.IsOwner, Is.False);
		});
	}

	private sealed class RecordingServer(IDaemon daemon, TypeCatalogue catalogue, TestEntityStorage storage)
		: RelayServer(daemon, catalogue, storage) {
		public List<(EntityMessageKind Kind, Guid Id, int? Value)> Messages { get; } = [];

		protected override void OnCreateEntityMessage(IRelayTransport sender, Guid entityId, Guid typeId, ReadOnlySpan<byte> data) {
			base.OnCreateEntityMessage(sender, entityId, typeId, data);
			Record(EntityMessageKind.Create, entityId);
		}

		protected override void OnUpdateEntityMessage(IRelayTransport sender, Guid entityId, ReadOnlySpan<byte> data) {
			base.OnUpdateEntityMessage(sender, entityId, data);
			Record(EntityMessageKind.Update, entityId);
		}

		private void Record(EntityMessageKind kind, Guid id) {
			int? value = storage.TryGetEntity(id, out NetworkEntity? entity) ? ((RelayValueState)entity).Value : null;
			Messages.Add((kind, id, value));
		}
	}
}
