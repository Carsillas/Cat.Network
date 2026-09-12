using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	public enum ObjectMessageParameterKind {
		NullableBase,
		Base,
		NullableDerived
	}

	[TestCase(false, ObjectMessageParameterKind.NullableBase, false)]
	[TestCase(false, ObjectMessageParameterKind.NullableBase, true)]
	[TestCase(false, ObjectMessageParameterKind.Base, false)]
	[TestCase(false, ObjectMessageParameterKind.NullableDerived, false)]
	[TestCase(false, ObjectMessageParameterKind.NullableDerived, true)]
	[TestCase(true, ObjectMessageParameterKind.NullableBase, false)]
	[TestCase(true, ObjectMessageParameterKind.NullableBase, true)]
	[TestCase(true, ObjectMessageParameterKind.Base, false)]
	[TestCase(true, ObjectMessageParameterKind.NullableDerived, false)]
	[TestCase(true, ObjectMessageParameterKind.NullableDerived, true)]
	public void RemoteMessagePreservesNetworkObjectParameter(bool broadcast, ObjectMessageParameterKind parameterKind, bool sendNull) {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(NetworkObjectMessageState), typeof(RelayMessagePayload));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		RelayServer server = new(daemon, catalogue, new TestEntityStorage());
		RelayClient ownerClient = new(catalogue);
		RelayClient observerClient = new(catalogue);
		ownerClient.Connect(daemon.Connect());
		observerClient.Connect(daemon.Connect());
		Pump(server, ownerClient, observerClient);

		NetworkObjectMessageState entity = new();
		ownerClient.Spawn(entity);
		Pump(server, ownerClient, observerClient);
		Assert.That(observerClient.TryGetEntity(entity.Id, out NetworkEntity? proxyEntity), Is.True);
		NetworkObjectMessageState proxy = (NetworkObjectMessageState)proxyEntity!;
		NetworkObjectMessageState sender = broadcast ? entity : proxy;
		NetworkObjectMessageState receiver = broadcast ? proxy : entity;
		RelayClient sendingClient = broadcast ? ownerClient : observerClient;
		RelayClient receivingClient = broadcast ? observerClient : ownerClient;

		int receivedCount = 0;
		NetworkObject? receivedValue = null;
		RelayClient? receivedClient = null;
		Guid receivedInstigatorId = Guid.Empty;
		void Receive(RelayClient client, NetworkProfile instigator, NetworkObject? value) {
			receivedCount++;
			receivedValue = value;
			receivedClient = client;
			receivedInstigatorId = instigator.Id;
		}

		RelayMessagePayload? payload = sendNull ? null : new RelayMessagePayload { Label = "Remote payload", Value = 42 };
		switch (broadcast, parameterKind) {
			case (false, ObjectMessageParameterKind.NullableBase):
				receiver.ApplyNullableBaseReceived += Receive;
				sender.ApplyNullableBase(payload);
				break;
			case (false, ObjectMessageParameterKind.Base):
				receiver.ApplyBaseReceived += Receive;
				sender.ApplyBase(payload!);
				break;
			case (false, ObjectMessageParameterKind.NullableDerived):
				receiver.ApplyNullableDerivedReceived += Receive;
				sender.ApplyNullableDerived(payload);
				break;
			case (true, ObjectMessageParameterKind.NullableBase):
				receiver.PublishNullableBaseReceived += Receive;
				sender.PublishNullableBase(payload);
				break;
			case (true, ObjectMessageParameterKind.Base):
				receiver.PublishBaseReceived += Receive;
				sender.PublishBase(payload!);
				break;
			case (true, ObjectMessageParameterKind.NullableDerived):
				receiver.PublishNullableDerivedReceived += Receive;
				sender.PublishNullableDerived(payload);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(parameterKind));
		}

		Assert.That(receivedCount, Is.Zero, "The receiver must be reached through the remote message path.");
		Pump(server, ownerClient, observerClient);

		Assert.Multiple(() => {
			Assert.That(receivedCount, Is.EqualTo(1));
			Assert.That(receivedClient, Is.SameAs(receivingClient));
			Assert.That(receivedInstigatorId, Is.EqualTo(sendingClient.Profile!.Id));
		});
		if (sendNull) {
			Assert.That(receivedValue, Is.Null);
		} else {
			Assert.That(receivedValue, Is.TypeOf<RelayMessagePayload>());
			RelayMessagePayload receivedPayload = (RelayMessagePayload)receivedValue!;
			Assert.Multiple(() => {
				Assert.That(receivedPayload, Is.Not.SameAs(payload));
				Assert.That(receivedPayload.Label, Is.EqualTo(payload!.Label));
				Assert.That(receivedPayload.Value, Is.EqualTo(payload.Value));
			});
		}
	}
}
