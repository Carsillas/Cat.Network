using System.Diagnostics.CodeAnalysis;
using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	[Test]
	public void RelayClientConnect_ReceivesProfileOutsideEntityStorage() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		TestEntityStorage serverStorage = new();
		RelayServer server = new(daemon, catalogue, serverStorage);
		RelayClient client = new(catalogue);

		client.Connect(daemon.Connect());
		Pump(server, client);

		Assert.Multiple(() => {
			Assert.That(client.Profile, Is.TypeOf<RelayProfileState>());
			Assert.That(client.TryGetProfile(client.Profile!.Id, out NetworkProfile? knownProfile), Is.True);
			Assert.That(knownProfile, Is.TypeOf<RelayProfileState>());
			Assert.That(serverStorage.TryGetEntity(client.Profile!.Id, out _), Is.False);
		});
	}

	[Test]
	public void RelayClientProfile_DoesNotSetLocalProfileWithoutAssignment() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState));
		ExposedRelayClient client = new(catalogue);
		Guid profileId = Guid.NewGuid();

		client.Receive(new MemoryRelayTransport(), BuildProfileMessage(catalogue, profileId, new RelayProfileState()));

		Assert.Multiple(() => {
			Assert.That(client.Profile, Is.Null);
			Assert.That(client.TryGetProfile(profileId, out NetworkProfile? knownProfile), Is.True);
			Assert.That(knownProfile, Is.TypeOf<RelayProfileState>());
		});
	}

	[Test]
	public void RelayClientConnect_ReceivesOtherClientProfiles() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		RelayServer server = new(daemon, catalogue, new TestEntityStorage());
		RelayClient firstClient = new(catalogue);
		RelayClient secondClient = new(catalogue);

		firstClient.Connect(daemon.Connect());
		Pump(server, firstClient);

		secondClient.Connect(daemon.Connect());
		Pump(server, firstClient, secondClient);

		Assert.Multiple(() => {
			Assert.That(firstClient.Profile, Is.Not.Null);
			Assert.That(secondClient.Profile, Is.Not.Null);
			Assert.That(firstClient.Profile!.Id, Is.Not.EqualTo(secondClient.Profile!.Id));
			Assert.That(firstClient.TryGetProfile(secondClient.Profile!.Id, out NetworkProfile? firstKnownSecondProfile), Is.True);
			Assert.That(secondClient.TryGetProfile(firstClient.Profile!.Id, out NetworkProfile? secondKnownFirstProfile), Is.True);
			Assert.That(firstKnownSecondProfile, Is.TypeOf<RelayProfileState>());
			Assert.That(secondKnownFirstProfile, Is.TypeOf<RelayProfileState>());
		});
	}

	[Test]
	public void RelayServerRemoveTransport_RemovesDisconnectedProfileOnTick() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		RelayServer server = new(daemon, catalogue, new TestEntityStorage());
		RelayClient firstClient = new(catalogue);
		RelayClient secondClient = new(catalogue);
		MemoryRelayTransport secondTransport = daemon.Connect();

		firstClient.Connect(daemon.Connect());
		secondClient.Connect(secondTransport);
		Pump(server, firstClient, secondClient);
		Guid disconnectedProfileId = secondClient.Profile!.Id;
		Assert.That(firstClient.TryGetProfile(disconnectedProfileId, out _), Is.True);

		server.RemoveTransport(secondTransport.Remote!);
		firstClient.Tick();
		Assert.That(firstClient.TryGetProfile(disconnectedProfileId, out _), Is.True);

		Pump(server, firstClient);

		Assert.That(firstClient.TryGetProfile(disconnectedProfileId, out _), Is.False);
	}

	[Test]
	public void RelayServerRemoveTransport_RemovesPendingOwnershipTransfer() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		TestEntityStorage serverStorage = new();
		ExposedRelayServer server = new(daemon, catalogue, serverStorage);
		RelayClient firstClient = new(catalogue);
		RelayClient secondClient = new(catalogue);
		MemoryRelayTransport firstTransport = daemon.Connect();
		MemoryRelayTransport secondTransport = daemon.Connect();

		firstClient.Connect(firstTransport);
		secondClient.Connect(secondTransport);
		Pump(server, firstClient, secondClient);

		RelayValueState entity = new() { Value = 3 };
		firstClient.Spawn(entity);
		Pump(server, firstClient, secondClient);
		Assert.That(serverStorage.TryGetEntity(entity.Id, out _), Is.True);

		server.Receive(firstTransport.Remote!, BuildOwnershipTransferRequestMessage(entity.Id, secondClient.Profile!.Id));

		server.RemoveTransport(secondTransport.Remote!);
		Pump(server, firstClient);

		entity.Value = 11;
		Pump(server, firstClient);

		Assert.Multiple(() => {
			Assert.That(entity.IsOwner, Is.True);
			Assert.That(serverStorage.TryGetEntity(entity.Id, out NetworkEntity? updatedEntity), Is.True);
			Assert.That(((RelayValueState)updatedEntity!).Value, Is.EqualTo(11));
		});
	}

	[Test]
	public void RelayServerRemoveTransport_DeletesDestroyWithOwnerEntity() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		TestEntityStorage serverStorage = new();
		RelayServer server = new(daemon, catalogue, serverStorage);
		RelayClient ownerClient = new(catalogue);
		RelayClient observerClient = new(catalogue);
		MemoryRelayTransport ownerTransport = daemon.Connect();

		ownerClient.Connect(ownerTransport);
		observerClient.Connect(daemon.Connect());
		Pump(server, ownerClient, observerClient);

		RelayValueState entity = new() { DestroyWithOwner = true, Value = 3 };
		ownerClient.Spawn(entity);
		Pump(server, ownerClient, observerClient);
		Assert.That(serverStorage.TryGetEntity(entity.Id, out NetworkEntity? serverEntity), Is.True);
		Assert.That(serverEntity!.DestroyWithOwner, Is.True);
		Assert.That(observerClient.TryGetEntity(entity.Id, out NetworkEntity? observerEntity), Is.True);
		Assert.That(observerEntity!.DestroyWithOwner, Is.True);

		server.RemoveTransport(ownerTransport.Remote!);
		Pump(server, observerClient);

		Assert.Multiple(() => {
			Assert.That(serverStorage.TryGetEntity(entity.Id, out _), Is.False);
			Assert.That(observerClient.TryGetEntity(entity.Id, out _), Is.False);
			Assert.That(serverEntity.IsSpawned, Is.False);
			Assert.That(observerEntity.IsSpawned, Is.False);
		});
	}

	[Test]
	public void RelayServerRemoveTransport_PreservesEntityWithoutDestroyWithOwner() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		TestEntityStorage serverStorage = new();
		RelayServer server = new(daemon, catalogue, serverStorage);
		RelayClient ownerClient = new(catalogue);
		RelayClient observerClient = new(catalogue);
		MemoryRelayTransport ownerTransport = daemon.Connect();

		ownerClient.Connect(ownerTransport);
		observerClient.Connect(daemon.Connect());
		Pump(server, ownerClient, observerClient);

		RelayValueState entity = new() { Value = 3 };
		ownerClient.Spawn(entity);
		Pump(server, ownerClient, observerClient);

		server.RemoveTransport(ownerTransport.Remote!);
		Pump(server, observerClient);

		Assert.Multiple(() => {
			Assert.That(serverStorage.TryGetEntity(entity.Id, out NetworkEntity? serverEntity), Is.True);
			Assert.That(serverEntity!.DestroyWithOwner, Is.False);
			Assert.That(observerClient.TryGetEntity(entity.Id, out NetworkEntity? observerEntity), Is.True);
			Assert.That(observerEntity!.IsOwner, Is.True);
		});
	}

	[Test]
	public void RelayClientSpawn_SendsCreateToServerAndOtherClients() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		TestEntityStorage serverStorage = new();
		RelayServer server = new(daemon, catalogue, serverStorage);
		RelayClient firstClient = new(catalogue);
		RelayClient secondClient = new(catalogue);

		firstClient.Connect(daemon.Connect());
		secondClient.Connect(daemon.Connect());
		Pump(server, firstClient, secondClient);

		RelayValueState entity = new() { Value = 3 };
		Assert.That(entity.IsSpawned, Is.False);

		firstClient.Spawn(entity);
		Pump(server, firstClient, secondClient);

		Assert.Multiple(() => {
			Assert.That(serverStorage.TryGetEntity(entity.Id, out NetworkEntity? serverEntity), Is.True);
			Assert.That(serverEntity, Is.TypeOf<RelayValueState>());
			Assert.That(((RelayValueState)serverEntity!).Value, Is.EqualTo(3));
			Assert.That(secondClient.TryGetEntity(entity.Id, out NetworkEntity? secondClientEntity), Is.True);
			Assert.That(secondClientEntity, Is.TypeOf<RelayValueState>());
			Assert.That(((RelayValueState)secondClientEntity!).Value, Is.EqualTo(3));
			Assert.That(entity.IsSpawned, Is.True);
			Assert.That(serverEntity.IsSpawned, Is.True);
			Assert.That(secondClientEntity.IsSpawned, Is.True);
			Assert.That(entity.IsOwner, Is.True);
			Assert.That(secondClientEntity!.IsOwner, Is.False);
		});
	}

	[Test]
	public void NetworkObjectIsOwner_DelegatesToEntityAnchor() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(PropertyChangedEntityState), typeof(DirtyChildState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		RelayServer server = new(daemon, catalogue, new TestEntityStorage());
		RelayClient ownerClient = new(catalogue);
		RelayClient observerClient = new(catalogue);

		ownerClient.Connect(daemon.Connect());
		observerClient.Connect(daemon.Connect());
		Pump(server, ownerClient, observerClient);

		PropertyChangedEntityState entity = new() {
			Child = new DirtyChildState()
		};
		ownerClient.Spawn(entity);
		Pump(server, ownerClient, observerClient);

		Assert.That(observerClient.TryGetEntity(entity.Id, out NetworkEntity? observerEntity), Is.True);
		PropertyChangedEntityState observerState = (PropertyChangedEntityState)observerEntity!;

		Assert.Multiple(() => {
			Assert.That(entity.IsOwner, Is.True);
			Assert.That(entity.Child!.IsOwner, Is.True);
			Assert.That(observerState.IsOwner, Is.False);
			Assert.That(observerState.Child!.IsOwner, Is.False);
		});
	}

	[Test]
	public void RelayRpc_NonOwnerInvocation_ForwardsToOwnerWithInstigatorProfile() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayMessageState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		RelayServer server = new(daemon, catalogue, new TestEntityStorage());
		RelayClient ownerClient = new(catalogue);
		RelayClient instigatorClient = new(catalogue);

		ownerClient.Connect(daemon.Connect());
		instigatorClient.Connect(daemon.Connect());
		Pump(server, ownerClient, instigatorClient);

		RelayMessageState entity = new();
		ownerClient.Spawn(entity);
		Pump(server, ownerClient, instigatorClient);
		Assert.That(instigatorClient.TryGetEntity(entity.Id, out NetworkEntity? proxyEntity), Is.True);

		int receivedValue = 0;
		Guid receivedInstigatorId = Guid.Empty;
		entity.ApplyValueReceived += (_, instigator, value) => {
			receivedInstigatorId = instigator.Id;
			receivedValue = value;
		};

		((RelayMessageState)proxyEntity!).ApplyValue(42);
		Pump(server, ownerClient, instigatorClient);

		Assert.Multiple(() => {
			Assert.That(receivedValue, Is.EqualTo(42));
			Assert.That(receivedInstigatorId, Is.EqualTo(instigatorClient.Profile!.Id));
		});
	}

	[Test]
	public void RelayRpc_NonOwnerInvocation_SerializesSupportedParameterTypesDirectly() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayMessageState), typeof(RelayMessagePayload));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		RelayServer server = new(daemon, catalogue, new TestEntityStorage());
		RelayClient ownerClient = new(catalogue);
		RelayClient instigatorClient = new(catalogue);

		ownerClient.Connect(daemon.Connect());
		instigatorClient.Connect(daemon.Connect());
		Pump(server, ownerClient, instigatorClient);

		RelayMessageState entity = new();
		ownerClient.Spawn(entity);
		Pump(server, ownerClient, instigatorClient);
		Assert.That(instigatorClient.TryGetEntity(entity.Id, out NetworkEntity? proxyEntity), Is.True);

		RelayMessagePayload? receivedPayload = null;
		RelayMessagePoint receivedPoint = default;
		int? receivedCount = null;
		entity.ApplyPayloadReceived += (_, _, payload, point, count) => {
			receivedPayload = payload;
			receivedPoint = point;
			receivedCount = count;
		};

		((RelayMessageState)proxyEntity!).ApplyPayload(
			new RelayMessagePayload { Label = "Payload", Value = 14 },
			new RelayMessagePoint { X = 9, Name = "North" },
			3);
		Pump(server, ownerClient, instigatorClient);

		Assert.Multiple(() => {
			Assert.That(receivedPayload, Is.Not.Null);
			Assert.That(receivedPayload!.Label, Is.EqualTo("Payload"));
			Assert.That(receivedPayload.Value, Is.EqualTo(14));
			Assert.That(receivedPoint.X, Is.EqualTo(9));
			Assert.That(receivedPoint.Name, Is.EqualTo("North"));
			Assert.That(receivedCount, Is.EqualTo(3));
		});
	}

	[Test]
	public void RelayBroadcast_OwnerInvocation_InvokesLocallyAndForwardsToOtherClients() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayMessageState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		RelayServer server = new(daemon, catalogue, new TestEntityStorage());
		RelayClient ownerClient = new(catalogue);
		RelayClient observerClient = new(catalogue);

		ownerClient.Connect(daemon.Connect());
		observerClient.Connect(daemon.Connect());
		Pump(server, ownerClient, observerClient);

		RelayMessageState entity = new();
		ownerClient.Spawn(entity);
		Pump(server, ownerClient, observerClient);
		Assert.That(observerClient.TryGetEntity(entity.Id, out NetworkEntity? proxyEntity), Is.True);

		int localCount = 0;
		int remoteCount = 0;
		int remoteValue = 0;
		Guid remoteInstigatorId = Guid.Empty;
		entity.PublishValueReceived += (_, _, _) => localCount++;
		((RelayMessageState)proxyEntity!).PublishValueReceived += (_, instigator, value) => {
			remoteCount++;
			remoteInstigatorId = instigator.Id;
			remoteValue = value;
		};

		entity.PublishValue(7);
		Assert.That(localCount, Is.EqualTo(1));

		Pump(server, ownerClient, observerClient);

		Assert.Multiple(() => {
			Assert.That(localCount, Is.EqualTo(1));
			Assert.That(remoteCount, Is.EqualTo(1));
			Assert.That(remoteValue, Is.EqualTo(7));
			Assert.That(remoteInstigatorId, Is.EqualTo(ownerClient.Profile!.Id));
		});
	}

	[Test]
	public void RelayBroadcast_NonOwnerInvocation_Throws() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayMessageState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		RelayServer server = new(daemon, catalogue, new TestEntityStorage());
		RelayClient ownerClient = new(catalogue);
		RelayClient observerClient = new(catalogue);

		ownerClient.Connect(daemon.Connect());
		observerClient.Connect(daemon.Connect());
		Pump(server, ownerClient, observerClient);

		RelayMessageState entity = new();
		ownerClient.Spawn(entity);
		Pump(server, ownerClient, observerClient);
		Assert.That(observerClient.TryGetEntity(entity.Id, out NetworkEntity? proxyEntity), Is.True);

		Assert.That(() => ((RelayMessageState)proxyEntity!).PublishValue(7), Throws.TypeOf<InvalidOperationException>());
	}

	[Test]
	public void RelayClientOwnedUpdate_SendsDirtyStateToServerAndOtherClients() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		TestEntityStorage serverStorage = new();
		RelayServer server = new(daemon, catalogue, serverStorage);
		RelayClient firstClient = new(catalogue);
		RelayClient secondClient = new(catalogue);

		firstClient.Connect(daemon.Connect());
		secondClient.Connect(daemon.Connect());
		Pump(server, firstClient, secondClient);

		RelayValueState entity = new() { Value = 3 };
		firstClient.Spawn(entity);
		Pump(server, firstClient, secondClient);

		entity.Value = 9;
		Pump(server, firstClient, secondClient);

		Assert.Multiple(() => {
			Assert.That(serverStorage.TryGetEntity(entity.Id, out NetworkEntity? serverEntity), Is.True);
			Assert.That(((RelayValueState)serverEntity!).Value, Is.EqualTo(9));
			Assert.That(secondClient.TryGetEntity(entity.Id, out NetworkEntity? secondClientEntity), Is.True);
			Assert.That(((RelayValueState)secondClientEntity!).Value, Is.EqualTo(9));
		});
	}

	[Test]
	public void RelayServerDirtyEntity_SendsUpdateToRelevantClients() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		TestEntityStorage serverStorage = new();
		RelayValueState entity = new() { Value = 3 };
		serverStorage.RegisterEntity(entity);
		RelayServer server = new(daemon, catalogue, serverStorage);
		RelayClient firstClient = new(catalogue);
		RelayClient secondClient = new(catalogue);

		firstClient.Connect(daemon.Connect());
		secondClient.Connect(daemon.Connect());
		Pump(server, firstClient, secondClient);
		Assert.That(firstClient.TryGetEntity(entity.Id, out NetworkEntity? firstClientEntity), Is.True);
		Assert.That(secondClient.TryGetEntity(entity.Id, out NetworkEntity? secondClientEntity), Is.True);

		entity.Value = 14;
		Pump(server, firstClient, secondClient);

		Assert.Multiple(() => {
			Assert.That(new[] { firstClientEntity!.IsOwner, secondClientEntity!.IsOwner }.Count(static isOwner => isOwner), Is.EqualTo(1));
			NetworkEntity observerEntity = firstClientEntity.IsOwner ? secondClientEntity : firstClientEntity;
			Assert.That(((RelayValueState)observerEntity).Value, Is.EqualTo(14));
		});
	}

	[Test]
	public void RelayClientDelete_RemovesEntityFromServerAndOtherClients() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		TestEntityStorage serverStorage = new();
		RelayServer server = new(daemon, catalogue, serverStorage);
		RelayClient firstClient = new(catalogue);
		RelayClient secondClient = new(catalogue);

		firstClient.Connect(daemon.Connect());
		secondClient.Connect(daemon.Connect());
		Pump(server, firstClient, secondClient);

		RelayValueState entity = new() { Value = 3 };
		firstClient.Spawn(entity);
		Pump(server, firstClient, secondClient);
		Assert.That(entity.IsSpawned, Is.True);
		Assert.That(secondClient.TryGetEntity(entity.Id, out NetworkEntity? secondClientEntity), Is.True);
		Assert.That(secondClientEntity!.IsSpawned, Is.True);

		firstClient.Delete(entity);
		Pump(server, firstClient, secondClient);

		Assert.Multiple(() => {
			Assert.That(serverStorage.TryGetEntity(entity.Id, out _), Is.False);
			Assert.That(firstClient.TryGetEntity(entity.Id, out _), Is.False);
			Assert.That(secondClient.TryGetEntity(entity.Id, out _), Is.False);
			Assert.That(entity.IsSpawned, Is.False);
			Assert.That(secondClientEntity.IsSpawned, Is.False);
		});
	}

	[Test]
	public void RelayServerDeleteEntityMessage_SendsDeleteDuringTickDiff() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		TestEntityStorage serverStorage = new();
		RelayValueState entity = new() { Value = 3 };
		serverStorage.RegisterEntity(entity);
		RecordingRelayTransport firstTransport = new();
		RecordingRelayTransport secondTransport = new();
		AcceptedRelayDaemon daemon = new(
			(firstTransport, new RelayProfileState()),
			(secondTransport, new RelayProfileState()));
		ExposedRelayServer server = new(daemon, catalogue, serverStorage);

		server.Tick();
		firstTransport.SentMessages.Clear();
		secondTransport.SentMessages.Clear();

		server.Receive(firstTransport, BuildDeleteEntityMessage(entity.Id));

		Assert.Multiple(() => {
			Assert.That(serverStorage.TryGetEntity(entity.Id, out _), Is.False);
			Assert.That(firstTransport.SentMessages, Is.Empty);
			Assert.That(secondTransport.SentMessages, Is.Empty);
		});

		server.Tick();

		Assert.Multiple(() => {
			Assert.That(firstTransport.SentMessages, Has.Count.EqualTo(1));
			Assert.That(secondTransport.SentMessages, Has.Count.EqualTo(1));
		});
	}

	[Test]
	public void RelayClientDelete_ThrowsWhenEntityIsNotOwned() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		RelayServer server = new(daemon, catalogue, new TestEntityStorage());
		RelayClient firstClient = new(catalogue);
		RelayClient secondClient = new(catalogue);

		firstClient.Connect(daemon.Connect());
		secondClient.Connect(daemon.Connect());
		Pump(server, firstClient, secondClient);

		RelayValueState entity = new() { Value = 3 };
		firstClient.Spawn(entity);
		Pump(server, firstClient, secondClient);
		Assert.That(secondClient.TryGetEntity(entity.Id, out NetworkEntity? secondClientEntity), Is.True);

		Assert.That(() => secondClient.Delete(secondClientEntity!), Throws.TypeOf<InvalidOperationException>());
	}

	[Test]
	public void RelayClientAssignOwner_ForfeitsOwnershipWhenNewOwnerDoesNotKnowEntity() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		RelevantEntityStorage serverStorage = new();
		RelayServer server = new(daemon, catalogue, serverStorage);
		RelayClient firstClient = new(catalogue);
		RelayClient secondClient = new(catalogue);

		firstClient.Connect(daemon.Connect());
		secondClient.Connect(daemon.Connect());
		Pump(server, firstClient, secondClient);

		RelayValueState entity = new() { Value = 3 };
		firstClient.Spawn(entity);
		serverStorage.Allow(firstClient.Profile!.Id, entity.Id);
		Pump(server, firstClient, secondClient);
		Assert.That(serverStorage.TryGetEntity(entity.Id, out NetworkEntity? serverEntity), Is.True);
		Assert.That(secondClient.TryGetEntity(entity.Id, out _), Is.False);

		firstClient.AssignOwner(entity, secondClient.Profile!.Id);
		Pump(server, firstClient, secondClient);

		Assert.Multiple(() => {
			Assert.That(entity.IsOwner, Is.True);
			Assert.That(secondClient.TryGetEntity(entity.Id, out _), Is.False);
		});
	}

	[Test]
	public void RelayClientAssignOwner_ReacquiresForfeitedRelevantEntityAndSendsUpdates() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		TestEntityStorage serverStorage = new();
		RelayServer server = new(daemon, catalogue, serverStorage);
		RelayClient client = new(catalogue);

		client.Connect(daemon.Connect());
		Pump(server, client);

		RelayValueState entity = new() { Value = 3 };
		client.Spawn(entity);
		Pump(server, client);
		Assert.That(entity.IsOwner, Is.True);

		client.AssignOwner(entity, Guid.NewGuid());
		Pump(server, client);
		Assert.That(entity.IsOwner, Is.True);

		entity.Value = 19;
		Pump(server, client);

		Assert.Multiple(() => {
			Assert.That(serverStorage.TryGetEntity(entity.Id, out NetworkEntity? serverEntity), Is.True);
			Assert.That(((RelayValueState)serverEntity!).Value, Is.EqualTo(19));
		});
	}

	[Test]
	public void RelayServer_AssignsOwnerlessRelevantEntityToClient() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		TestEntityStorage serverStorage = new();
		RelayValueState entity = new() { Value = 7 };
		serverStorage.RegisterEntity(entity);
		RelayServer server = new(daemon, catalogue, serverStorage);
		RelayClient client = new(catalogue);

		client.Connect(daemon.Connect());
		Pump(server, client);

		Assert.Multiple(() => {
			Assert.That(client.TryGetEntity(entity.Id, out NetworkEntity? clientEntity), Is.True);
			Assert.That(clientEntity, Is.TypeOf<RelayValueState>());
			Assert.That(((RelayValueState)clientEntity!).Value, Is.EqualTo(7));
			Assert.That(clientEntity.IsOwner, Is.True);
		});
	}

	[Test]
	public void RelayClientAssignOwner_SendsTransferRequestToServer() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		TestEntityStorage serverStorage = new();
		RelayServer server = new(daemon, catalogue, serverStorage);
		RelayClient firstClient = new(catalogue);
		RelayClient secondClient = new(catalogue);

		firstClient.Connect(daemon.Connect());
		secondClient.Connect(daemon.Connect());
		Pump(server, firstClient, secondClient);

		RelayValueState entity = new() { Value = 3 };
		firstClient.Spawn(entity);
		Pump(server, firstClient, secondClient);

		firstClient.AssignOwner(entity, secondClient.Profile!.Id);
		Assert.That(entity.IsOwner, Is.True);

		Pump(server, firstClient, secondClient);

		Assert.Multiple(() => {
			Assert.That(entity.IsOwner, Is.False);
			Assert.That(secondClient.TryGetEntity(entity.Id, out NetworkEntity? secondClientEntity), Is.True);
			Assert.That(secondClientEntity!.IsOwner, Is.True);
		});
	}

	[Test]
	public void RelayClientAssignOwner_SendsDirtyUpdateBeforeTransferRequest() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		TestEntityStorage serverStorage = new();
		RelayServer server = new(daemon, catalogue, serverStorage);
		RelayClient firstClient = new(catalogue);
		RelayClient secondClient = new(catalogue);

		firstClient.Connect(daemon.Connect());
		secondClient.Connect(daemon.Connect());
		Pump(server, firstClient, secondClient);

		RelayValueState entity = new() { Value = 3 };
		firstClient.Spawn(entity);
		Pump(server, firstClient, secondClient);

		entity.Value = 11;
		firstClient.AssignOwner(entity, secondClient.Profile!.Id);
		Pump(server, firstClient, secondClient);

		Assert.Multiple(() => {
			Assert.That(serverStorage.TryGetEntity(entity.Id, out NetworkEntity? serverEntity), Is.True);
			Assert.That(((RelayValueState)serverEntity!).Value, Is.EqualTo(11));
			Assert.That(secondClient.TryGetEntity(entity.Id, out NetworkEntity? secondClientEntity), Is.True);
			Assert.That(((RelayValueState)secondClientEntity!).Value, Is.EqualTo(11));
			Assert.That(secondClientEntity.IsOwner, Is.True);
			Assert.That(entity.IsOwner, Is.False);
		});
	}

	[Test]
	public void RelayClientDelete_DiscardsPendingOwnershipTransferRequest() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		TestEntityStorage serverStorage = new();
		RelayServer server = new(daemon, catalogue, serverStorage);
		RelayClient firstClient = new(catalogue);
		RelayClient secondClient = new(catalogue);

		firstClient.Connect(daemon.Connect());
		secondClient.Connect(daemon.Connect());
		Pump(server, firstClient, secondClient);

		RelayValueState entity = new() { Value = 3 };
		firstClient.Spawn(entity);
		Pump(server, firstClient, secondClient);

		firstClient.AssignOwner(entity, secondClient.Profile!.Id);
		firstClient.Delete(entity);
		Pump(server, firstClient, secondClient);

		Assert.Multiple(() => {
			Assert.That(serverStorage.TryGetEntity(entity.Id, out _), Is.False);
			Assert.That(firstClient.TryGetEntity(entity.Id, out _), Is.False);
			Assert.That(secondClient.TryGetEntity(entity.Id, out _), Is.False);
		});
	}

	[Test]
	public void RelayClientAssignOwner_ThrowsWhenEntityIsNotOwned() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		RelayServer server = new(daemon, catalogue, new TestEntityStorage());
		RelayClient firstClient = new(catalogue);
		RelayClient secondClient = new(catalogue);

		firstClient.Connect(daemon.Connect());
		secondClient.Connect(daemon.Connect());
		Pump(server, firstClient, secondClient);

		RelayValueState entity = new() { Value = 3 };
		firstClient.Spawn(entity);
		Pump(server, firstClient, secondClient);
		Assert.That(secondClient.TryGetEntity(entity.Id, out NetworkEntity? secondClientEntity), Is.True);

		Assert.That(() => secondClient.AssignOwner(secondClientEntity!, firstClient.Profile!.Id), Throws.TypeOf<InvalidOperationException>());
	}

	[Test]
	public void RelayServer_DoesNotSendCreateForIrrelevantEntity() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		RelevantEntityStorage serverStorage = new();
		RelayServer server = new(daemon, catalogue, serverStorage);
		RelayClient firstClient = new(catalogue);
		RelayClient secondClient = new(catalogue);

		firstClient.Connect(daemon.Connect());
		secondClient.Connect(daemon.Connect());
		Pump(server, firstClient, secondClient);

		RelayValueState entity = new() { Value = 3 };
		firstClient.Spawn(entity);
		Pump(server, firstClient, secondClient);

		Assert.Multiple(() => {
			Assert.That(serverStorage.TryGetEntity(entity.Id, out _), Is.True);
			Assert.That(secondClient.TryGetEntity(entity.Id, out _), Is.False);
		});
	}

	[Test]
	public void RelayServer_DoesNotKeepOwnedEntityRelevant() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		RelevantEntityStorage serverStorage = new();
		RelayServer server = new(daemon, catalogue, serverStorage);
		RelayClient client = new(catalogue);

		client.Connect(daemon.Connect());
		Pump(server, client);

		RelayValueState entity = new() { Value = 3 };
		client.Spawn(entity);
		Pump(server, client);

		Assert.Multiple(() => {
			Assert.That(serverStorage.TryGetEntity(entity.Id, out _), Is.True);
			Assert.That(client.TryGetEntity(entity.Id, out _), Is.False);
			Assert.That(entity.IsOwner, Is.False);
		});
	}

	[Test]
	public void RelayServer_SendsCreateWhenEntityBecomesRelevant() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		RelevantEntityStorage serverStorage = new();
		RelayServer server = new(daemon, catalogue, serverStorage);
		RelayClient firstClient = new(catalogue);
		RelayClient secondClient = new(catalogue);

		firstClient.Connect(daemon.Connect());
		secondClient.Connect(daemon.Connect());
		Pump(server, firstClient, secondClient);

		RelayValueState entity = new() { Value = 3 };
		firstClient.Spawn(entity);
		Pump(server, firstClient, secondClient);

		serverStorage.Allow(secondClient.Profile!.Id, entity.Id);
		Pump(server, firstClient, secondClient);

		Assert.Multiple(() => {
			Assert.That(secondClient.TryGetEntity(entity.Id, out NetworkEntity? secondClientEntity), Is.True);
			Assert.That(secondClientEntity, Is.TypeOf<RelayValueState>());
			Assert.That(((RelayValueState)secondClientEntity!).Value, Is.EqualTo(3));
		});
	}

	[Test]
	public void RelayServer_SendsDeleteWhenEntityStopsBeingRelevant() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		RelevantEntityStorage serverStorage = new();
		RelayServer server = new(daemon, catalogue, serverStorage);
		RelayClient firstClient = new(catalogue);
		RelayClient secondClient = new(catalogue);

		firstClient.Connect(daemon.Connect());
		secondClient.Connect(daemon.Connect());
		Pump(server, firstClient, secondClient);

		RelayValueState entity = new() { Value = 3 };
		firstClient.Spawn(entity);
		Pump(server, firstClient, secondClient);

		serverStorage.Allow(secondClient.Profile!.Id, entity.Id);
		Pump(server, firstClient, secondClient);
		Assert.That(secondClient.TryGetEntity(entity.Id, out _), Is.True);

		serverStorage.Deny(secondClient.Profile.Id, entity.Id);
		Pump(server, firstClient, secondClient);

		Assert.That(secondClient.TryGetEntity(entity.Id, out _), Is.False);
	}

	private static byte[] BuildProfileMessage(TypeCatalogue catalogue, Guid profileId, NetworkProfile profile) {
		byte[] payload = Serialize(profile, catalogue);
		Assert.That(catalogue.TryFindTypeId(profile.GetType(), out Guid typeId), Is.True);
		return Concat(
			[(byte)NetworkMessageChannel.ProfileMessage],
			[(byte)ProfileMessageKind.CreateOrUpdate],
			GuidBytes(profileId),
			GuidBytes(typeId),
			Int32(payload.Length),
			payload);
	}

	private static byte[] BuildDeleteEntityMessage(Guid entityId) {
		return Concat(
			[(byte)NetworkMessageChannel.EntityMessage],
			[(byte)EntityMessageKind.Delete],
			GuidBytes(entityId));
	}

	private static byte[] BuildOwnershipTransferRequestMessage(Guid entityId, Guid ownerProfileId) {
		return Concat(
			[(byte)NetworkMessageChannel.EntityMessage],
			[(byte)EntityMessageKind.RequestOwnershipTransfer],
			GuidBytes(entityId),
			GuidBytes(ownerProfileId));
	}

	private sealed class ExposedRelayClient(TypeCatalogue typeCatalogue) : RelayClient(typeCatalogue) {
		public void Receive(IRelayTransport sender, ReadOnlySpan<byte> message) {
			ProcessMessage(sender, message);
		}
	}

	private sealed class ExposedRelayServer(IDaemon daemon, TypeCatalogue typeCatalogue, IEntityStorage entityStorage)
		: RelayServer(daemon, typeCatalogue, entityStorage) {

		public void Receive(IRelayTransport sender, ReadOnlySpan<byte> message) {
			ProcessMessage(sender, message);
		}
	}

	private sealed class RecordingRelayTransport : IRelayTransport {
		public List<byte[]> SentMessages { get; } = [];

		public event MessageHandler? MessageReceived;

		public void Send(ReadOnlySpan<byte> message) {
			SentMessages.Add(message.ToArray());
		}

		public void PumpMessages() {
		}

		public void Receive(ReadOnlySpan<byte> message) {
			MessageReceived?.Invoke(this, message);
		}
	}

	private sealed class AcceptedRelayDaemon(params (IRelayTransport Transport, NetworkProfile Profile)[] connections) : IDaemon {
		private Queue<(IRelayTransport Transport, NetworkProfile Profile)> Connections { get; } = new(connections);

		public void Tick() {
		}

		public bool TryAcceptConnection([NotNullWhen(true)] out IRelayTransport? transport, [NotNullWhen(true)] out NetworkProfile? profile) {
			if (Connections.TryDequeue(out (IRelayTransport Transport, NetworkProfile Profile) connection)) {
				transport = connection.Transport;
				profile = connection.Profile;
				return true;
			}

			transport = null;
			profile = null;
			return false;
		}
	}
}


