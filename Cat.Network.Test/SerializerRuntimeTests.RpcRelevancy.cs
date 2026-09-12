using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	[Test]
	public void RelayRpcRelevancy_DropsPacketFromCallerThatNeverObservedEntity() {
		RpcRelevancyScenario scenario = new(callerRelevant: false);
		byte[] packet = BuildRelevancyRpcMessage(scenario.Catalogue, scenario.Entity.Id, 42);

		scenario.CallerTransport.Send(packet);
		scenario.Pump();

		scenario.AssertNoRpcForwarded();
	}

	[Test]
	public void RelayRpcRelevancy_DropsPacketAfterCallerUnobservesEntity() {
		RpcRelevancyScenario scenario = new(callerRelevant: true);
		Assert.That(scenario.Caller.TryGetEntity(scenario.Entity.Id, out NetworkEntity? observedEntity), Is.True);
		byte[] packet = BuildRelevancyRpcMessage(scenario.Catalogue, scenario.Entity.Id, 42);

		scenario.Storage.Deny(scenario.Caller.Profile!.Id, scenario.Entity.Id);
		scenario.Pump();
		Assert.Multiple(() => {
			Assert.That(scenario.Caller.TryGetEntity(scenario.Entity.Id, out _), Is.False);
			Assert.That(observedEntity!.IsSpawned, Is.False);
			Assert.That(scenario.Entity.IsOwner, Is.True);
		});

		scenario.CallerTransport.Send(packet);
		scenario.Pump();

		scenario.AssertNoRpcForwarded();
	}

	[TestCase(false)]
	[TestCase(true)]
	public void RelayRpcRelevancy_ForwardsKnownNonOwnerPacketWithInstigator(bool reobserve) {
		RpcRelevancyScenario scenario = new(callerRelevant: reobserve);
		if (reobserve) {
			scenario.Storage.Deny(scenario.Caller.Profile!.Id, scenario.Entity.Id);
			scenario.Pump();
			Assert.That(scenario.Caller.TryGetEntity(scenario.Entity.Id, out _), Is.False);
		}

		scenario.Storage.Allow(scenario.Caller.Profile!.Id, scenario.Entity.Id);
		scenario.Pump();
		Assert.That(scenario.Caller.TryGetEntity(scenario.Entity.Id, out NetworkEntity? proxy), Is.True);
		Assert.That(proxy!.IsOwner, Is.False);
		byte[] packet = BuildRelevancyRpcMessage(scenario.Catalogue, scenario.Entity.Id, 42);

		scenario.CallerTransport.Send(packet);
		scenario.Pump();

		Assert.Multiple(() => {
			Assert.That(scenario.ForwardedToOwner, Has.Count.EqualTo(1));
			Assert.That(scenario.ForwardedToCaller, Is.Empty);
			Assert.That(scenario.ReceivedRpcs, Is.EqualTo(new[] { (scenario.Caller.Profile.Id, 42) }));
		});
	}

	[Test]
	public void RelayRpcRelevancy_OwnerInvocationRemainsLocal() {
		RpcRelevancyScenario scenario = new(callerRelevant: true);

		scenario.Entity.ApplyValue(42);

		Assert.That(scenario.ReceivedRpcs, Is.EqualTo(new[] { (scenario.Owner.Profile!.Id, 42) }));
		scenario.Pump();
		Assert.Multiple(() => {
			Assert.That(scenario.ReceivedRpcs, Has.Count.EqualTo(1));
			Assert.That(scenario.ForwardedToOwner, Is.Empty);
			Assert.That(scenario.ForwardedToCaller, Is.Empty);
		});
	}

	[Test]
	public void RelayRpcRelevancy_DropsPacketSentByOwner() {
		RpcRelevancyScenario scenario = new(callerRelevant: true);
		byte[] packet = BuildRelevancyRpcMessage(scenario.Catalogue, scenario.Entity.Id, 42);

		scenario.OwnerTransport.Send(packet);
		scenario.Pump();

		scenario.AssertNoRpcForwarded();
	}

	[Test]
	public void RelayRpcRelevancy_DropsPacketForRemovedEntityBeforeRelevancyUpdates() {
		RpcRelevancyScenario scenario = new(callerRelevant: true);
		byte[] packet = BuildRelevancyRpcMessage(scenario.Catalogue, scenario.Entity.Id, 42);
		Assert.That(scenario.Storage.UnregisterEntity(scenario.Entity.Id), Is.True);

		// Incoming messages are processed before the next tick removes the clients' known entity ids.
		scenario.CallerTransport.Send(packet);
		scenario.Pump();

		scenario.AssertNoRpcForwarded();
	}

	[Test]
	public void RelayRpcRelevancy_DropsPacketFromUnrecognizedTransport() {
		RpcRelevancyScenario scenario = new(callerRelevant: true);
		byte[] packet = BuildRelevancyRpcMessage(scenario.Catalogue, scenario.Entity.Id, 42);

		scenario.Server.Receive(new RecordingRelayTransport(), packet);
		scenario.Pump();

		scenario.AssertNoRpcForwarded();
	}

	[Test]
	public void RelayRpcRelevancy_DropsPacketAfterOwnerDisconnectsBeforeReassignment() {
		RpcRelevancyScenario scenario = new(callerRelevant: true);
		byte[] packet = BuildRelevancyRpcMessage(scenario.Catalogue, scenario.Entity.Id, 42);
		scenario.Server.RemoveTransport(scenario.OwnerTransport.Remote!);
		Assert.That(scenario.Storage.TryGetEntity(scenario.Entity.Id, out _), Is.True);
		Assert.That(scenario.Caller.TryGetEntity(scenario.Entity.Id, out _), Is.True);

		// Deliver while the caller still knows the entity and its owner has not been replaced.
		scenario.Server.Receive(scenario.CallerTransport.Remote!, packet);
		scenario.Pump();

		scenario.AssertNoRpcForwarded();
	}

	private static byte[] BuildRelevancyRpcMessage(TypeCatalogue catalogue, Guid entityId, int value) {
		// Build a valid generated RPC independently of the real caller's observed entities.
		RecordingRelayTransport transport = new();
		ExposedRelayClient client = new(catalogue);
		client.Connect(transport);
		client.Receive(transport, BuildCreateEntityMessage(catalogue, entityId, new RelayMessageState()));
		Assert.That(client.TryGetEntity(entityId, out NetworkEntity? entity), Is.True);
		((RelayMessageState)entity!).ApplyValue(value);
		client.Tick();
		return transport.SentMessages.Single();
	}

	private sealed class RpcRelevancyScenario {
		public TypeCatalogue Catalogue { get; } = RegisterTypes(typeof(RelayProfileState), typeof(RelayMessageState));
		public RelevantEntityStorage Storage { get; } = new();
		public ExposedRelayServer Server { get; }
		public RelayClient Owner { get; }
		public RelayClient Caller { get; }
		public MemoryRelayTransport OwnerTransport { get; }
		public MemoryRelayTransport CallerTransport { get; }
		public RelayMessageState Entity { get; } = new();
		public List<byte[]> ForwardedToOwner { get; } = [];
		public List<byte[]> ForwardedToCaller { get; } = [];
		public List<(Guid InstigatorId, int Value)> ReceivedRpcs { get; } = [];

		public RpcRelevancyScenario(bool callerRelevant) {
			int profileValue = 0;
			MemoryRelayDaemon daemon = new(() => new RelayProfileState { Value = ++profileValue });
			Server = new ExposedRelayServer(daemon, Catalogue, Storage);
			Owner = new RelayClient(Catalogue);
			Caller = new RelayClient(Catalogue);
			OwnerTransport = daemon.Connect();
			CallerTransport = daemon.Connect();
			Owner.Connect(OwnerTransport);
			Caller.Connect(CallerTransport);
			Pump();

			Owner.Spawn(Entity);
			Storage.Allow(Owner.Profile!.Id, Entity.Id);
			if (callerRelevant) {
				Storage.Allow(Caller.Profile!.Id, Entity.Id);
			}
			Pump();
			Assert.Multiple(() => {
				Assert.That(Owner.TryGetEntity(Entity.Id, out _), Is.True);
				Assert.That(Entity.IsOwner, Is.True);
				Assert.That(Caller.TryGetEntity(Entity.Id, out _), Is.EqualTo(callerRelevant));
			});

			Entity.ApplyValueReceived += (_, instigator, value) => ReceivedRpcs.Add((instigator.Id, value));
			OwnerTransport.MessageReceived += (_, message) => RecordRpc(message, ForwardedToOwner);
			CallerTransport.MessageReceived += (_, message) => RecordRpc(message, ForwardedToCaller);
		}

		public void Pump() {
			SerializerRuntimeTests.Pump(Server, Owner, Caller);
		}

		public void AssertNoRpcForwarded() {
			Assert.Multiple(() => {
				Assert.That(ForwardedToOwner, Is.Empty);
				Assert.That(ForwardedToCaller, Is.Empty);
				Assert.That(ReceivedRpcs, Is.Empty);
			});
		}

		private static void RecordRpc(ReadOnlySpan<byte> message, List<byte[]> packets) {
			if (message.Length >= 2 &&
			    message[0] == (byte)NetworkMessageChannel.EntityMessage &&
			    message[1] == (byte)EntityMessageKind.Rpc) {
				packets.Add(message.ToArray());
			}
		}
	}
}
