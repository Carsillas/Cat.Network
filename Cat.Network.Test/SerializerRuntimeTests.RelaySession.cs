using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	[TestCase(false)]
	[TestCase(true)]
	public void RelayClientSessionEnd_ClearsStateBeforeLifecycleNotifications(bool remoteDisconnect) {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		RelayClient client = new(catalogue);
		SessionRelayTransport transport = new();
		client.Connect(transport);
		Guid ownProfileId = Guid.NewGuid();
		Guid remoteProfileId = Guid.NewGuid();
		transport.Receive(BuildProfileMessage(catalogue, ownProfileId, new RelayProfileState { Value = 1 }));
		transport.Receive(BuildAssignProfileMessage(ownProfileId));
		transport.Receive(BuildProfileMessage(catalogue, remoteProfileId, new RelayProfileState { Value = 2 }));
		RelayValueState owned = new() { Value = 3 };
		client.Spawn(owned);
		Guid observedId = Guid.NewGuid();
		transport.Receive(BuildCreateEntityMessage(catalogue, observedId, new RelayValueState { Value = 4 }));
		Assert.That(client.TryGetEntity(observedId, out NetworkEntity? observed), Is.True);
		List<string> notifications = [];
		List<bool> clearedBeforeNotification = [];
		void Record(string notification) {
			notifications.Add(notification);
			clearedBeforeNotification.Add(client.Profile is null &&
				!client.TryGetProfile(ownProfileId, out _) && !client.TryGetProfile(remoteProfileId, out _) &&
				!client.TryGetEntity(owned.Id, out _) && !client.TryGetEntity(observedId, out _) &&
				!owned.IsSpawned && !owned.IsOwner && !observed!.IsSpawned && !observed.IsOwner);
		}
		client.EntityOwnershipLost += entity => Record($"lost:{entity.Id}");
		client.EntityUnobserved += entity => Record($"unobserved:{entity.Id}");
		client.ProfileLeft += profile => Record($"left:{profile.Id}");

		if (remoteDisconnect) {
			transport.Disconnect();
		} else {
			client.Disconnect();
		}
		client.Disconnect();
		transport.Disconnect();
		transport.Receive(BuildCreateEntityMessage(catalogue, Guid.NewGuid(), new RelayValueState { Value = 5 }));

		Assert.Multiple(() => {
			Assert.That(notifications, Is.EquivalentTo(new[] {
				$"lost:{owned.Id}", $"unobserved:{owned.Id}", $"unobserved:{observedId}",
				$"left:{ownProfileId}", $"left:{remoteProfileId}"
			}));
			Assert.That(clearedBeforeNotification, Is.All.True);
			Assert.That(transport.MessageSubscriberCount, Is.Zero);
			Assert.That(transport.DisconnectSubscriberCount, Is.Zero);
			Assert.That(transport.Disposed, Is.False);
		});
	}

	[TestCase("explicit")]
	[TestCase("remote")]
	[TestCase("replace")]
	public void RelayClientSessionEnd_DiscardsEveryKindOfQueuedOutput(string ending) {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState), typeof(RelayMessageState));
		RelayClient client = new(catalogue);
		SessionRelayTransport oldTransport = new();
		SessionRelayTransport newTransport = new();
		client.Connect(oldTransport);
		Guid profileId = Guid.NewGuid();
		oldTransport.Receive(BuildProfileMessage(catalogue, profileId, new RelayProfileState()));
		oldTransport.Receive(BuildAssignProfileMessage(profileId));
		RelayValueState deleted = new() { Value = 1 };
		RelayValueState transferred = new() { Value = 2 };
		RelayMessageState broadcaster = new();
		client.Spawn(deleted);
		client.Spawn(transferred);
		client.Spawn(broadcaster);
		client.Tick();
		oldTransport.SentMessages.Clear();
		Guid observedId = Guid.NewGuid();
		oldTransport.Receive(BuildCreateEntityMessage(catalogue, observedId, new RelayMessageState { DestroyWithOwner = true }));
		Assert.That(client.TryGetEntity(observedId, out NetworkEntity? observed), Is.True);
		RelayValueState pendingSpawn = new() { Value = 3 };
		client.Spawn(pendingSpawn);
		client.Delete(deleted);
		client.AssignOwner(transferred, Guid.NewGuid());
		broadcaster.PublishValue(42);
		((RelayMessageState)observed!).ApplyValue(6);
		((RelayProfileState)client.Profile!).Value = 7;
		BufferWriter queuedWriter = client.RentRpcMessageWriter(observed, 123, out _);
		client.QueueRentedMessageWriter(queuedWriter);

		if (ending == "explicit") {
			client.Disconnect();
		} else if (ending == "remote") {
			oldTransport.Disconnect();
		}
		client.Connect(newTransport);
		client.Tick();

		Assert.Multiple(() => {
			Assert.That(oldTransport.SentMessages, Is.Empty);
			Assert.That(newTransport.SentMessages, Is.Empty);
			Assert.That(queuedWriter.WrittenCount, Is.Zero);
			Assert.That(pendingSpawn.IsSpawned, Is.False);
			Assert.That(deleted.IsOwner, Is.False);
			Assert.That(transferred.IsOwner, Is.False);
			Assert.That(client.Profile, Is.Null);
			Assert.That(oldTransport.Disposed, Is.False);
		});

		RelayValueState fresh = new() { Value = 9 };
		client.Spawn(fresh);
		client.Tick();
		Assert.That(newTransport.SentMessages, Has.Count.EqualTo(1));
		Assert.That(newTransport.SentMessages[0], Is.EqualTo(BuildCreateEntityMessage(catalogue, fresh.Id, fresh)));
	}

	[Test]
	public void RelayClientSessionReconnect_ToNewServerRequiresExplicitRespawn() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon firstDaemon = new(() => new RelayProfileState { Value = 1 });
		MemoryRelayDaemon secondDaemon = new(() => new RelayProfileState { Value = 2 });
		TestEntityStorage firstStorage = new();
		TestEntityStorage secondStorage = new();
		RelayServer firstServer = new(firstDaemon, catalogue, firstStorage);
		RelayServer secondServer = new(secondDaemon, catalogue, secondStorage);
		RelayClient client = new(catalogue);
		client.Connect(firstDaemon.Connect());
		Pump(firstServer, client);
		NetworkProfile oldProfile = client.Profile!;
		RelayValueState oldEntity = new() { Value = 7 };
		client.Spawn(oldEntity);
		Pump(firstServer, client);
		Assert.That(firstStorage.TryGetEntity(oldEntity.Id, out _), Is.True);

		client.Connect(secondDaemon.Connect());
		Pump(secondServer, client);

		Assert.Multiple(() => {
			Assert.That(client.TryGetEntity(oldEntity.Id, out _), Is.False);
			Assert.That(client.TryGetProfile(oldProfile.Id, out _), Is.False);
			Assert.That(oldEntity.IsSpawned, Is.False);
			Assert.That(oldEntity.IsOwner, Is.False);
			Assert.That(client.Profile, Is.Not.SameAs(oldProfile));
			Assert.That(((RelayProfileState)client.Profile!).Value, Is.EqualTo(2));
			Assert.That(secondStorage.TryGetEntity(oldEntity.Id, out _), Is.False);
		});

		client.Spawn(oldEntity);
		Pump(secondServer, client);
		Assert.That(secondStorage.TryGetEntity(oldEntity.Id, out NetworkEntity? respawned), Is.True);
		Assert.That(((RelayValueState)respawned!).Value, Is.EqualTo(7));
	}

	[Test]
	public void RelayClientSessionConnect_InvalidTransportPreservesCurrentSession() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState), typeof(RelayProfileState));
		RelayClient client = new(catalogue);
		SessionRelayTransport transport = new();
		client.Connect(transport);
		RelayValueState entity = new() { Value = 1 };
		client.Spawn(entity);

		Assert.That(() => client.Connect(null!), Throws.ArgumentNullException);
		Guid profileId = Guid.NewGuid();
		transport.Receive(BuildProfileMessage(catalogue, profileId, new RelayProfileState()));
		transport.Receive(BuildAssignProfileMessage(profileId));
		client.Tick();

		Assert.Multiple(() => {
			Assert.That(client.Profile?.Id, Is.EqualTo(profileId));
			Assert.That(entity.IsSpawned, Is.True);
			Assert.That(entity.IsOwner, Is.True);
			Assert.That(transport.SentMessages, Has.Count.EqualTo(1));
			Assert.That(transport.MessageSubscriberCount, Is.EqualTo(1));
			Assert.That(transport.DisconnectSubscriberCount, Is.EqualTo(1));
		});
	}

	[Test]
	public void RelayClientSessionConnect_PreservesPreparedEntitiesAndSameTransportSession() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		RelayClient client = new(catalogue);
		SessionRelayTransport transport = new();
		RelayValueState entity = new() { Value = 1 };
		client.Spawn(entity);
		client.Connect(transport);
		client.Connect(transport);
		client.Tick();

		Assert.Multiple(() => {
			Assert.That(client.TryGetEntity(entity.Id, out NetworkEntity? registered), Is.True);
			Assert.That(registered, Is.SameAs(entity));
			Assert.That(entity.IsOwner, Is.True);
			Assert.That(transport.SentMessages, Has.Count.EqualTo(1));
			Assert.That(transport.MessageSubscriberCount, Is.EqualTo(1));
			Assert.That(transport.DisconnectSubscriberCount, Is.EqualTo(1));
		});
	}

	[Test]
	public void RelayClientSessionDisconnect_BeforeFirstConnectDiscardsPreparedEntities() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		RelayClient client = new(catalogue);
		RelayValueState entity = new() { Value = 1 };
		client.Spawn(entity);
		client.Disconnect();
		SessionRelayTransport transport = new();
		client.Connect(transport);
		client.Tick();

		Assert.Multiple(() => {
			Assert.That(entity.IsSpawned, Is.False);
			Assert.That(entity.IsOwner, Is.False);
			Assert.That(transport.SentMessages, Is.Empty);
		});
	}

	[Test]
	public void RelayClientSessionTick_DisconnectDuringPumpStopsOutgoingWork() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		RelayClient client = new(catalogue);
		SessionRelayTransport transport = new();
		client.Connect(transport);
		RelayValueState entity = new() { Value = 1 };
		client.Spawn(entity);
		transport.OnPump = transport.Disconnect;

		Assert.That(client.Tick, Throws.Nothing);
		Assert.Multiple(() => {
			Assert.That(transport.SentMessages, Is.Empty);
			Assert.That(entity.IsSpawned, Is.False);
		});
	}

	[Test]
	public void RelayClientSessionSpawn_ReconnectFromObservedCallbackDoesNotRestoreOldSpawn() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		RelayClient client = new(catalogue);
		SessionRelayTransport oldTransport = new();
		SessionRelayTransport newTransport = new();
		client.Connect(oldTransport);
		RelayValueState oldEntity = new() { Value = 1 };
		RelayValueState newEntity = new() { Value = 2 };
		List<Guid> ownershipGained = [];
		client.EntityOwnershipGained += entity => ownershipGained.Add(entity.Id);
		client.EntityObserved += entity => {
			if (ReferenceEquals(entity, oldEntity)) {
				client.Connect(newTransport);
				client.Spawn(newEntity);
			}
		};

		client.Spawn(oldEntity);
		client.Tick();

		Assert.Multiple(() => {
			Assert.That(oldEntity.IsSpawned, Is.False);
			Assert.That(oldEntity.IsOwner, Is.False);
			Assert.That(ownershipGained, Is.EqualTo(new[] { newEntity.Id }));
			Assert.That(newTransport.SentMessages, Has.Count.EqualTo(1));
			Assert.That(newTransport.SentMessages[0], Is.EqualTo(BuildCreateEntityMessage(catalogue, newEntity.Id, newEntity)));
		});
	}

	[Test]
	public void RelayClientSessionConnect_ReentrantConnectionFromTeardownTakesPrecedence() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		RelayClient client = new(catalogue);
		SessionRelayTransport oldTransport = new();
		SessionRelayTransport requestedTransport = new();
		SessionRelayTransport callbackTransport = new();
		client.Connect(oldTransport);
		RelayValueState oldEntity = new() { Value = 1 };
		RelayValueState newEntity = new() { Value = 2 };
		client.Spawn(oldEntity);
		client.EntityUnobserved += entity => {
			if (ReferenceEquals(entity, oldEntity)) {
				client.Connect(callbackTransport);
				client.Spawn(newEntity);
			}
		};

		client.Connect(requestedTransport);
		client.Tick();

		Assert.Multiple(() => {
			Assert.That(oldEntity.IsSpawned, Is.False);
			Assert.That(newEntity.IsSpawned, Is.True);
			Assert.That(callbackTransport.SentMessages, Has.Count.EqualTo(1));
			Assert.That(requestedTransport.SentMessages, Is.Empty);
			Assert.That(requestedTransport.MessageSubscriberCount, Is.Zero);
		});
	}

	[TestCase("profile")]
	[TestCase("update")]
	[TestCase("spawn")]
	[TestCase("delete")]
	[TestCase("message")]
	[TestCase("transfer")]
	public void RelayClientSessionTick_ReconnectDuringSendStopsOldTick(string outputKind) {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		RelayClient client = new(catalogue);
		SessionRelayTransport oldTransport = new();
		SessionRelayTransport newTransport = new();
		client.Connect(oldTransport);
		Guid profileId = Guid.NewGuid();
		oldTransport.Receive(BuildProfileMessage(catalogue, profileId, new RelayProfileState()));
		oldTransport.Receive(BuildAssignProfileMessage(profileId));
		RelayValueState entity = new() { Value = 1 };
		client.Spawn(entity);
		client.Tick();
		oldTransport.SentMessages.Clear();
		switch (outputKind) {
			case "profile": ((RelayProfileState)client.Profile!).Value = 2; break;
			case "update": entity.Value = 2; break;
			case "spawn": client.Spawn(new RelayValueState { Value = 2 }); break;
			case "delete": client.Delete(entity); break;
			case "message": client.QueueRentedMessageWriter(client.RentRpcMessageWriter(entity, 123, out _)); break;
			case "transfer": client.AssignOwner(entity, Guid.NewGuid()); break;
		}
		RelayValueState newEntity = new() { Value = 3 };
		oldTransport.OnSend = () => {
			client.Connect(newTransport);
			client.Spawn(newEntity);
			client.Tick();
		};

		Assert.That(client.Tick, Throws.Nothing);
		Assert.Multiple(() => {
			Assert.That(oldTransport.SentMessages, Has.Count.EqualTo(1));
			Assert.That(newTransport.SentMessages, Is.Empty);
			Assert.That(entity.IsSpawned, Is.False);
			Assert.That(newEntity.IsOwner, Is.True);
		});
		client.Tick();
		Assert.That(newTransport.SentMessages, Has.Count.EqualTo(1));
		Assert.That(newTransport.SentMessages[0], Is.EqualTo(BuildCreateEntityMessage(catalogue, newEntity.Id, newEntity)));
	}

	[Test]
	public void RelayClientSessionReceive_ReconnectFromObservedCallbackPreservesNewSessionEdits() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		RelayClient client = new(catalogue);
		SessionRelayTransport oldTransport = new();
		SessionRelayTransport newTransport = new();
		client.Connect(oldTransport);
		RelayValueState? reusedEntity = null;
		client.EntityObserved += entity => {
			if (reusedEntity is null) {
				reusedEntity = (RelayValueState)entity;
				client.Connect(newTransport);
				reusedEntity.Value = 42;
				client.Spawn(entity);
			}
		};
		Guid entityId = Guid.NewGuid();
		oldTransport.OnPump = () => oldTransport.Receive(BuildCreateEntityMessage(catalogue, entityId, new RelayValueState { Value = 1 }));

		client.Tick();

		Assert.Multiple(() => {
			Assert.That(reusedEntity, Is.Not.Null);
			Assert.That(reusedEntity!.IsOwner, Is.True);
			Assert.That(reusedEntity.Value, Is.EqualTo(42));
			Assert.That(((INetworkObject)reusedEntity).PropertyStates.Any(state => state != NetworkPropertyState.Unchanged), Is.True);
			Assert.That(oldTransport.SentMessages, Is.Empty);
			Assert.That(newTransport.SentMessages, Is.Empty);
		});
		client.Tick();
		Assert.That(newTransport.SentMessages, Has.Count.EqualTo(1));
		Assert.That(newTransport.SentMessages[0], Is.EqualTo(BuildCreateEntityMessage(catalogue, entityId, reusedEntity!)));
	}

	[TestCase(false)]
	[TestCase(true)]
	public void RelayClientSessionTick_ReconnectFromOutgoingLifecycleCallbackPreservesNewWork(bool transferOwnership) {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		RelayClient client = new(catalogue);
		SessionRelayTransport oldTransport = new();
		SessionRelayTransport newTransport = new();
		client.Connect(oldTransport);
		RelayValueState oldEntity = new() { Value = 1 };
		RelayValueState newEntity = new() { Value = 2 };
		client.Spawn(oldEntity);
		client.Tick();
		oldTransport.SentMessages.Clear();
		void Reconnect(NetworkEntity entity) {
			if (ReferenceEquals(entity, oldEntity)) {
				client.Connect(newTransport);
				client.Spawn(newEntity);
			}
		}
		if (transferOwnership) {
			client.EntityOwnershipLost += Reconnect;
			client.AssignOwner(oldEntity, Guid.NewGuid());
		} else {
			client.EntityUnobserved += Reconnect;
			client.Delete(oldEntity);
		}

		Assert.That(client.Tick, Throws.Nothing);
		Assert.Multiple(() => {
			Assert.That(oldEntity.IsSpawned, Is.False);
			Assert.That(newEntity.IsOwner, Is.True);
			Assert.That(oldTransport.SentMessages, Has.Count.EqualTo(1));
			Assert.That(newTransport.SentMessages, Is.Empty);
		});
		client.Tick();
		Assert.That(newTransport.SentMessages, Has.Count.EqualTo(1));
		Assert.That(newTransport.SentMessages[0], Is.EqualTo(BuildCreateEntityMessage(catalogue, newEntity.Id, newEntity)));
	}

	[Test]
	public void RelayClientSessionTick_SendFailureAfterDisconnectReturnsInFlightWriter() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		RelayClient client = new(catalogue);
		SessionRelayTransport oldTransport = new();
		client.Connect(oldTransport);
		RelayValueState entity = new() { Value = 1 };
		client.Spawn(entity);
		client.Tick();
		BufferWriter writer = client.RentRpcMessageWriter(entity, 123, out _);
		client.QueueRentedMessageWriter(writer);
		oldTransport.OnSend = () => {
			oldTransport.Disconnect();
			throw new IOException("Connection lost while sending.");
		};

		Assert.That(client.Tick, Throws.TypeOf<IOException>());
		SessionRelayTransport newTransport = new();
		client.Connect(newTransport);
		client.Tick();
		Assert.Multiple(() => {
			Assert.That(writer.WrittenCount, Is.Zero);
			Assert.That(entity.IsSpawned, Is.False);
			Assert.That(newTransport.SentMessages, Is.Empty);
		});
	}

	[Test]
	public void RelayClientSessionBroadcast_ReconnectFromLocalHandlerDoesNotQueueOldInvocation() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayMessageState));
		RelayClient client = new(catalogue);
		SessionRelayTransport oldTransport = new();
		SessionRelayTransport newTransport = new();
		client.Connect(oldTransport);
		Guid oldProfileId = Guid.NewGuid();
		oldTransport.Receive(BuildProfileMessage(catalogue, oldProfileId, new RelayProfileState()));
		oldTransport.Receive(BuildAssignProfileMessage(oldProfileId));
		RelayMessageState entity = new();
		client.Spawn(entity);
		client.Tick();
		oldTransport.SentMessages.Clear();
		Guid newProfileId = Guid.NewGuid();
		Guid? localInstigatorId = null;
		entity.PublishValueReceived += (_, instigator, _) => {
			localInstigatorId = instigator.Id;
			client.Connect(newTransport);
			newTransport.Receive(BuildProfileMessage(catalogue, newProfileId, new RelayProfileState()));
			newTransport.Receive(BuildAssignProfileMessage(newProfileId));
			client.Spawn(entity);
		};

		Assert.That(() => entity.PublishValue(42), Throws.InvalidOperationException);
		client.Tick();

		Assert.Multiple(() => {
			Assert.That(localInstigatorId, Is.EqualTo(oldProfileId));
			Assert.That(client.Profile?.Id, Is.EqualTo(newProfileId));
			Assert.That(oldTransport.SentMessages, Is.Empty);
			Assert.That(newTransport.SentMessages, Has.Count.EqualTo(1));
			Assert.That(newTransport.SentMessages[0], Is.EqualTo(BuildCreateEntityMessage(catalogue, entity.Id, entity)));
		});
	}

	[Test]
	public void RelayClientSessionReconnect_RejectsOldCallbacksAndUnqueuedWriterEvenForSameTransport() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		RelayClient client = new(catalogue);
		SessionRelayTransport transport = new();
		client.Connect(transport);
		RelayValueState entity = new() { Value = 1 };
		client.Spawn(entity);
		BufferWriter oldWriter = client.RentRpcMessageWriter(entity, 123, out _);
		MessageHandler oldMessageHandler = transport.CaptureMessageHandler()!;
		Action<IRelayTransport>? oldDisconnectHandler = transport.CaptureDisconnectHandler();
		client.Disconnect();
		client.Connect(transport);
		Guid oldProfileId = Guid.NewGuid();

		oldMessageHandler(transport, BuildProfileMessage(catalogue, oldProfileId, new RelayProfileState()));
		oldDisconnectHandler?.Invoke(transport);
		Assert.That(() => client.QueueRentedMessageWriter(oldWriter), Throws.InvalidOperationException);
		Guid newProfileId = Guid.NewGuid();
		transport.Receive(BuildProfileMessage(catalogue, newProfileId, new RelayProfileState()));
		transport.Receive(BuildAssignProfileMessage(newProfileId));
		client.Tick();

		Assert.Multiple(() => {
			Assert.That(client.TryGetProfile(oldProfileId, out _), Is.False);
			Assert.That(client.Profile?.Id, Is.EqualTo(newProfileId));
			Assert.That(transport.SentMessages, Is.Empty);
		});
	}

	private sealed class SessionRelayTransport : IRelayTransport, IDisposable {
		public List<byte[]> SentMessages { get; } = [];
		public Action? OnPump { get; set; }
		public Action? OnSend { get; set; }
		public bool Disposed { get; private set; }
		public event MessageHandler? MessageReceived;
		public event Action<IRelayTransport>? Disconnected;
		public int MessageSubscriberCount => MessageReceived?.GetInvocationList().Length ?? 0;
		public int DisconnectSubscriberCount => Disconnected?.GetInvocationList().Length ?? 0;
		public MessageHandler? CaptureMessageHandler() => MessageReceived;
		public Action<IRelayTransport>? CaptureDisconnectHandler() => Disconnected;
		public void Receive(ReadOnlySpan<byte> message) => MessageReceived?.Invoke(this, message);
		public void Disconnect() => Disconnected?.Invoke(this);
		public void PumpMessages() => OnPump?.Invoke();
		public void Dispose() => Disposed = true;
		public void Send(ReadOnlySpan<byte> message) {
			SentMessages.Add(message.ToArray());
			OnSend?.Invoke();
		}
	}
}
