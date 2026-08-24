using System.Buffers.Binary;
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
	public void EntityStorage_RegisterEntity_AssignsNetworkId() {
		TestEntityStorage storage = new();
		RelayValueState entity = new();

		bool registered = storage.RegisterEntity(entity);

		Assert.Multiple(() => {
			Assert.That(registered, Is.True);
			Assert.That(entity.Id, Is.Not.EqualTo(Guid.Empty));
			Assert.That(entity.IsSpawned, Is.False);
			Assert.That(storage.TryGetEntity(entity.Id, out NetworkEntity? storedEntity), Is.True);
			Assert.That(storedEntity, Is.SameAs(entity));
		});
	}

	[Test]
	public void EntityStorage_AssignNetworkId_PreservesLoadedId() {
		LoadingEntityStorage storage = new();
		RelayValueState entity = new();
		Guid id = Guid.NewGuid();

		bool registered = storage.LoadEntity(id, entity);

		Assert.Multiple(() => {
			Assert.That(registered, Is.True);
			Assert.That(entity.Id, Is.EqualTo(id));
			Assert.That(storage.TryGetEntity(id, out NetworkEntity? storedEntity), Is.True);
			Assert.That(storedEntity, Is.SameAs(entity));
		});
	}

	[Test]
	public void EntityStorage_AssignNetworkId_AllowsDetachedReassignment() {
		LoadingEntityStorage storage = new();
		RelayValueState entity = new();
		Guid originalId = Guid.NewGuid();
		Guid reassignedId = Guid.NewGuid();
		storage.SetNetworkId(entity, originalId);

		storage.SetNetworkId(entity, reassignedId);

		Assert.That(entity.Id, Is.EqualTo(reassignedId));
	}

	[Test]
	public void EntityStorage_AssignNetworkId_AllowsEmptyIdForDetachedEntity() {
		LoadingEntityStorage storage = new();
		RelayValueState entity = new();
		storage.SetNetworkId(entity, Guid.NewGuid());

		storage.SetNetworkId(entity, Guid.Empty);
		bool registered = storage.RegisterEntity(entity);

		Assert.Multiple(() => {
			Assert.That(registered, Is.True);
			Assert.That(entity.Id, Is.Not.EqualTo(Guid.Empty));
			Assert.That(storage.TryGetEntity(entity.Id, out NetworkEntity? storedEntity), Is.True);
			Assert.That(storedEntity, Is.SameAs(entity));
		});
	}

	[Test]
	public void RelayServer_AttachesPreRegisteredStorageEntities() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		LoadingEntityStorage storage = new();
		RelayValueState entity = new();
		Guid id = Guid.NewGuid();
		storage.LoadEntity(id, entity);

		new RelayServer(daemon, catalogue, storage);

		Assert.Multiple(() => {
			Assert.That(entity.Id, Is.EqualTo(id));
			Assert.That(entity.IsSpawned, Is.True);
			Assert.That(entity.IsOwner, Is.True);
			Assert.That(storage.TryGetEntity(id, out NetworkEntity? storedEntity), Is.True);
			Assert.That(storedEntity, Is.SameAs(entity));
		});
	}

	[Test]
	public void EntityStorage_RegisterEntity_ThrowsForAttachedEntity() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		TestEntityStorage storage = new();
		RelayValueState entity = new();
		storage.RegisterEntity(entity);
		new RelayServer(daemon, catalogue, storage);

		Assert.That(() => storage.RegisterEntity(entity), Throws.InvalidOperationException);
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
	public void RelayClientProfileJoined_FiresAfterRemoteCreateRegistersProfile() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState));
		ExposedRelayClient client = new(catalogue);
		Guid profileId = Guid.NewGuid();
		NetworkProfile? joinedProfile = null;
		bool couldFindJoinedProfile = false;
		int joinedCount = 0;

		client.ProfileJoined += joined => {
			joinedCount++;
			joinedProfile = joined;
			couldFindJoinedProfile = client.TryGetProfile(joined.Id, out NetworkProfile? registered) &&
			                         ReferenceEquals(registered, joined);
		};

		client.Receive(new MemoryRelayTransport(), BuildProfileMessage(catalogue, profileId, new RelayProfileState { Value = 7 }));
		client.Receive(new MemoryRelayTransport(), BuildProfileMessage(catalogue, profileId, new RelayProfileState { Value = 11 }));

		Assert.Multiple(() => {
			Assert.That(joinedCount, Is.EqualTo(1));
			Assert.That(joinedProfile, Is.TypeOf<RelayProfileState>());
			Assert.That(joinedProfile!.Id, Is.EqualTo(profileId));
			Assert.That(((RelayProfileState)joinedProfile).Value, Is.EqualTo(7));
			Assert.That(couldFindJoinedProfile, Is.True);
		});
	}

	[Test]
	public void RelayClientProfileLeft_FiresWithRemovedProfileForRemoteDelete() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState));
		ExposedRelayClient client = new(catalogue);
		Guid profileId = Guid.NewGuid();
		NetworkProfile? joinedProfile = null;
		NetworkProfile? leftProfile = null;
		bool couldFindLeftProfile = true;
		bool profileClearedBeforeEvent = false;
		int leftCount = 0;

		client.ProfileJoined += joined => joinedProfile = joined;
		client.ProfileLeft += left => {
			leftCount++;
			leftProfile = left;
			couldFindLeftProfile = client.TryGetProfile(left.Id, out _);
			profileClearedBeforeEvent = client.Profile is null;
		};

		client.Receive(new MemoryRelayTransport(), BuildProfileMessage(catalogue, profileId, new RelayProfileState { Value = 7 }));
		client.Receive(new MemoryRelayTransport(), BuildAssignProfileMessage(profileId));
		client.Receive(new MemoryRelayTransport(), BuildDeleteProfileMessage(profileId));
		client.Receive(new MemoryRelayTransport(), BuildDeleteProfileMessage(profileId));

		Assert.Multiple(() => {
			Assert.That(leftCount, Is.EqualTo(1));
			Assert.That(leftProfile, Is.SameAs(joinedProfile));
			Assert.That(couldFindLeftProfile, Is.False);
			Assert.That(profileClearedBeforeEvent, Is.True);
		});
	}

	[Test]
	public void RelayClientProfileLifecycleEvents_ContainSubscriberExceptions() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState));
		ExposedRelayClient client = new(catalogue);
		Guid profileId = Guid.NewGuid();
		int joinedCount = 0;
		int leftCount = 0;
		List<Exception> handlerExceptions = [];

		client.EventHandlerException += _ => throw new InvalidOperationException("Exception handler failed.");
		client.EventHandlerException += handlerExceptions.Add;
		client.ProfileJoined += _ => throw new InvalidOperationException("Profile joined handler failed.");
		client.ProfileJoined += _ => joinedCount++;
		client.ProfileLeft += _ => throw new InvalidOperationException("Profile left handler failed.");
		client.ProfileLeft += _ => leftCount++;

		Assert.That(() => client.Receive(new MemoryRelayTransport(), BuildProfileMessage(catalogue, profileId, new RelayProfileState())), Throws.Nothing);
		Assert.That(() => client.Receive(new MemoryRelayTransport(), BuildDeleteProfileMessage(profileId)), Throws.Nothing);

		Assert.Multiple(() => {
			Assert.That(joinedCount, Is.EqualTo(1));
			Assert.That(leftCount, Is.EqualTo(1));
			Assert.That(handlerExceptions.Select(static exception => exception.Message), Is.EqualTo(new[] {
				"Profile joined handler failed.",
				"Profile left handler failed."
			}));
		});
	}

	[Test]
	public void RelayClientProfileLifecycleEvents_RemoveUnsubscribesHandler() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState));
		ExposedRelayClient client = new(catalogue);
		int joinedCount = 0;

		void OnJoined(NetworkProfile _) {
			joinedCount++;
		}

		client.ProfileJoined += OnJoined;
		client.ProfileJoined -= OnJoined;
		client.Receive(new MemoryRelayTransport(), BuildProfileMessage(catalogue, Guid.NewGuid(), new RelayProfileState()));

		Assert.That(joinedCount, Is.EqualTo(0));
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
	public void RelayClientProfileUpdate_SendsOwnProfileToOtherClients() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		RelayServer server = new(daemon, catalogue, new TestEntityStorage());
		RelayClient firstClient = new(catalogue);
		RelayClient secondClient = new(catalogue);

		firstClient.Connect(daemon.Connect());
		secondClient.Connect(daemon.Connect());
		Pump(server, firstClient, secondClient);

		RelayProfileState firstProfile = (RelayProfileState)firstClient.Profile!;
		firstProfile.Value = 17;
		Pump(server, firstClient, secondClient);

		Assert.Multiple(() => {
			Assert.That(firstClient.Profile, Is.SameAs(firstProfile));
			Assert.That(((RelayProfileState)firstClient.Profile!).Value, Is.EqualTo(17));
			Assert.That(secondClient.TryGetProfile(firstClient.Profile!.Id, out NetworkProfile? secondKnownFirstProfile), Is.True);
			Assert.That(((RelayProfileState)secondKnownFirstProfile!).Value, Is.EqualTo(17));
		});
	}

	[Test]
	public void RelayServerProfileRelevancy_SendsPartialUpdateForKnownProfiles() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfilePatchState));
		RelayProfilePatchState firstProfile = new() { UntouchedValue = 41 };
		RelayProfilePatchState secondProfile = new();
		ThrowingRelayTransport firstTransport = new();
		ThrowingRelayTransport secondTransport = new();
		AcceptedRelayDaemon daemon = new(
			(firstTransport, firstProfile),
			(secondTransport, secondProfile));
		RelayServer server = new(daemon, catalogue, new TestEntityStorage());

		server.Tick();
		firstTransport.SentMessages.Clear();
		secondTransport.SentMessages.Clear();

		firstProfile.Value = 17;
		byte[] expectedPayload = Serialize(firstProfile, catalogue, MemberIdentificationMode.Index, MemberSelectionMode.Dirty);
		byte[] fullPayload = Serialize(firstProfile, catalogue);

		server.Tick();

		byte[] updateMessage = secondTransport.SentMessages.Single(message =>
			message[0] == (byte)NetworkMessageChannel.ProfileMessage &&
			message[1] == (byte)ProfileMessageKind.Update &&
			new Guid(message.AsSpan(2, 16)) == firstProfile.Id);
		int payloadLength = BinaryPrimitives.ReadInt32LittleEndian(updateMessage.AsSpan(18, 4));
		byte[] updatePayload = updateMessage.AsSpan(22, payloadLength).ToArray();

		Assert.Multiple(() => {
			Assert.That(updatePayload, Is.EqualTo(expectedPayload));
			Assert.That(updatePayload, Is.Not.EqualTo(fullPayload));
		});
	}

	[Test]
	public void RelayClientProfileUpdate_DoesNotSendAssignedProfileId() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState));
		ExposedRelayClient client = new(catalogue);
		RecordingRelayTransport transport = new();
		Guid profileId = Guid.NewGuid();

		client.Connect(transport);
		client.Receive(transport, BuildProfileMessage(catalogue, profileId, new RelayProfileState()));
		client.Receive(transport, BuildAssignProfileMessage(profileId));
		((RelayProfileState)client.Profile!).Value = 17;

		client.Tick();

		Assert.Multiple(() => {
			Assert.That(transport.SentMessages, Has.Count.EqualTo(1));
			byte[] message = transport.SentMessages.Single();
			Assert.That(message[0], Is.EqualTo((byte)NetworkMessageChannel.ProfileMessage));
			Assert.That(message[1], Is.EqualTo((byte)ProfileMessageKind.UpdateRequest));
			Assert.That(new Guid(message.AsSpan(2, 16)), Is.EqualTo(GetTypeId(typeof(RelayProfileState))));
			Assert.That(ContainsGuid(message, profileId), Is.False);
		});
	}

	[Test]
	public void RelayServerProfileUpdate_IgnoresUpdatesForOtherProfiles() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		ExposedRelayServer server = new(daemon, catalogue, new TestEntityStorage());
		RelayClient firstClient = new(catalogue);
		RelayClient secondClient = new(catalogue);
		MemoryRelayTransport firstTransport = daemon.Connect();

		firstClient.Connect(firstTransport);
		secondClient.Connect(daemon.Connect());
		Pump(server, firstClient, secondClient);

		server.Receive(
			firstTransport.Remote!,
			BuildProfileMessage(catalogue, secondClient.Profile!.Id, new RelayProfileState { Value = 99 }));
		Pump(server, firstClient, secondClient);

		Assert.Multiple(() => {
			Assert.That(((RelayProfileState)secondClient.Profile!).Value, Is.EqualTo(0));
			Assert.That(firstClient.TryGetProfile(secondClient.Profile.Id, out NetworkProfile? firstKnownSecondProfile), Is.True);
			Assert.That(((RelayProfileState)firstKnownSecondProfile!).Value, Is.EqualTo(0));
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
	public void RelayServerTransportDisconnected_RemovesDisconnectedProfileOnTick() {
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

		secondTransport.Remote!.Disconnect();
		Pump(server, firstClient);

		Assert.That(firstClient.TryGetProfile(disconnectedProfileId, out _), Is.False);
	}

	[Test]
	public void RelayServerTransportPumpFailure_RemovesDisconnectedProfileOnTick() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState));
		ThrowingRelayTransport firstTransport = new();
		ThrowingRelayTransport secondTransport = new();
		RelayProfileState firstProfile = new();
		RelayProfileState secondProfile = new();
		AcceptedRelayDaemon daemon = new(
			(firstTransport, firstProfile),
			(secondTransport, secondProfile));
		RelayServer server = new(daemon, catalogue, new TestEntityStorage());

		server.Tick();
		Guid disconnectedProfileId = secondProfile.Id;
		Assert.That(firstTransport.SentMessages.Any(message => ContainsGuid(message, disconnectedProfileId)), Is.True);

		firstTransport.SentMessages.Clear();
		secondTransport.ThrowOnPump = true;
		server.Tick();

		Assert.That(firstTransport.SentMessages.Any(message => ContainsGuid(message, disconnectedProfileId)), Is.True);
	}

	[Test]
	public void RelayServerTransportSendFailure_RemovesDisconnectedProfileOnTick() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		TestEntityStorage serverStorage = new();
		ThrowingRelayTransport firstTransport = new();
		ThrowingRelayTransport secondTransport = new();
		RelayProfileState firstProfile = new();
		RelayProfileState secondProfile = new();
		AcceptedRelayDaemon daemon = new(
			(firstTransport, firstProfile),
			(secondTransport, secondProfile));
		RelayServer server = new(daemon, catalogue, serverStorage);

		server.Tick();
		Guid disconnectedProfileId = secondProfile.Id;
		Assert.That(firstTransport.SentMessages.Any(message => ContainsGuid(message, disconnectedProfileId)), Is.True);

		firstTransport.SentMessages.Clear();
		secondTransport.ThrowOnSend = true;
		serverStorage.RegisterEntity(new RelayValueState { Value = 3 });
		server.Tick();
		server.Tick();

		Assert.That(firstTransport.SentMessages.Any(message => ContainsGuid(message, disconnectedProfileId)), Is.True);
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
	public void RelayServerCreateEntity_ThrowsForEmptyClientEntityId() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		ExposedRelayServer server = new(daemon, catalogue, new TestEntityStorage());
		RelayClient client = new(catalogue);
		MemoryRelayTransport transport = daemon.Connect();

		client.Connect(transport);
		Pump(server, client);

		Assert.That(
			() => server.Receive(
				transport.Remote!,
				BuildCreateEntityMessage(catalogue, Guid.Empty, new RelayValueState { Value = 7 })),
			Throws.InvalidOperationException);
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
	public void RelayClientEntityObserved_FiresAfterLocalSpawnRegistersEntity() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		RelayClient client = new(catalogue);
		RelayValueState entity = new() { Value = 3 };
		NetworkEntity? observedEntity = null;
		bool couldFindObservedEntity = false;
		int observedCount = 0;

		client.EntityObserved += observed => {
			observedCount++;
			observedEntity = observed;
			couldFindObservedEntity = client.TryGetEntity(observed.Id, out NetworkEntity? registered) &&
			                          ReferenceEquals(registered, observed);
		};

		client.Spawn(entity);
		client.Spawn(entity);

		Assert.Multiple(() => {
			Assert.That(observedCount, Is.EqualTo(1));
			Assert.That(observedEntity, Is.SameAs(entity));
			Assert.That(couldFindObservedEntity, Is.True);
		});
	}

	[Test]
	public void RelayClientEntityObserved_FiresAfterRemoteCreateRegistersEntity() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		ExposedRelayClient client = new(catalogue);
		Guid entityId = Guid.NewGuid();
		NetworkEntity? observedEntity = null;
		bool couldFindObservedEntity = false;
		int observedCount = 0;

		client.EntityObserved += observed => {
			observedCount++;
			observedEntity = observed;
			couldFindObservedEntity = client.TryGetEntity(observed.Id, out NetworkEntity? registered) &&
			                          ReferenceEquals(registered, observed);
		};

		client.Receive(new MemoryRelayTransport(), BuildCreateEntityMessage(catalogue, entityId, new RelayValueState { Value = 7 }));
		client.Receive(new MemoryRelayTransport(), BuildCreateEntityMessage(catalogue, entityId, new RelayValueState { Value = 11 }));

		Assert.Multiple(() => {
			Assert.That(observedCount, Is.EqualTo(1));
			Assert.That(observedEntity, Is.TypeOf<RelayValueState>());
			Assert.That(observedEntity!.Id, Is.EqualTo(entityId));
			Assert.That(((RelayValueState)observedEntity).Value, Is.EqualTo(7));
			Assert.That(couldFindObservedEntity, Is.True);
		});
	}

	[Test]
	public void RelayClientEntityUnobserved_FiresWithRemovedEntityForLocalAndRemoteDeletes() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		RelayClient localClient = new(catalogue);
		RecordingRelayTransport transport = new();
		RelayValueState localEntity = new() { Value = 3 };
		List<NetworkEntity> unobservedEntities = [];
		List<bool> couldFindUnobservedEntities = [];

		localClient.Connect(transport);
		localClient.EntityUnobserved += unobserved => {
			unobservedEntities.Add(unobserved);
			couldFindUnobservedEntities.Add(localClient.TryGetEntity(unobserved.Id, out _));
		};

		localClient.Spawn(localEntity);
		localClient.Tick();
		localClient.Delete(localEntity);
		localClient.Tick();
		localClient.Tick();

		ExposedRelayClient remoteClient = new(catalogue);
		Guid remoteEntityId = Guid.NewGuid();
		NetworkEntity? remoteObservedEntity = null;
		int remoteUnobservedCount = 0;
		bool couldFindRemoteUnobservedEntity = true;

		remoteClient.EntityObserved += observed => remoteObservedEntity = observed;
		remoteClient.EntityUnobserved += unobserved => {
			remoteUnobservedCount++;
			couldFindRemoteUnobservedEntity = remoteClient.TryGetEntity(unobserved.Id, out _);
			unobservedEntities.Add(unobserved);
		};

		remoteClient.Receive(new MemoryRelayTransport(), BuildCreateEntityMessage(catalogue, remoteEntityId, new RelayValueState { Value = 5 }));
		remoteClient.Receive(new MemoryRelayTransport(), BuildDeleteEntityMessage(remoteEntityId));
		remoteClient.Receive(new MemoryRelayTransport(), BuildDeleteEntityMessage(remoteEntityId));

		Assert.Multiple(() => {
			Assert.That(unobservedEntities[0], Is.SameAs(localEntity));
			Assert.That(couldFindUnobservedEntities[0], Is.False);
			Assert.That(remoteUnobservedCount, Is.EqualTo(1));
			Assert.That(unobservedEntities[1], Is.SameAs(remoteObservedEntity));
			Assert.That(couldFindRemoteUnobservedEntity, Is.False);
		});
	}

	[Test]
	public void RelayClientEntityOwnershipEvents_FireOnlyWhenOwnershipActuallyChanges() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		ExposedRelayClient client = new(catalogue);
		RecordingRelayTransport transport = new();
		RelayValueState entity = new() { Value = 3 };
		int gainedCount = 0;
		int lostCount = 0;
		List<NetworkEntity> gainedEntities = [];
		List<NetworkEntity> lostEntities = [];

		client.Connect(transport);
		client.EntityOwnershipGained += owned => {
			gainedEntities.Add(owned);
			gainedCount++;
		};
		client.EntityOwnershipLost += unowned => {
			lostEntities.Add(unowned);
			lostCount++;
		};

		client.Spawn(entity);
		client.Spawn(entity);
		client.Tick();

		client.Receive(new MemoryRelayTransport(), BuildAssignOwnerMessage(entity.Id));

		client.AssignOwner(entity, Guid.NewGuid());
		client.Tick();
		client.Tick();

		client.Receive(new MemoryRelayTransport(), BuildAssignOwnerMessage(entity.Id));
		client.Receive(new MemoryRelayTransport(), BuildAssignOwnerMessage(entity.Id));

		Assert.Multiple(() => {
			Assert.That(gainedCount, Is.EqualTo(2));
			Assert.That(lostCount, Is.EqualTo(1));
			Assert.That(gainedEntities, Is.EqualTo(new[] { entity, entity }));
			Assert.That(lostEntities, Is.EqualTo(new[] { entity }));
			Assert.That(entity.IsOwner, Is.True);
		});
	}

	[Test]
	public void RelayClientEntityLifecycleEvents_ContainSubscriberExceptions() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		ExposedRelayClient client = new(catalogue);
		RecordingRelayTransport transport = new();
		RelayValueState entity = new() { Value = 3 };
		int observedCount = 0;
		int unobservedCount = 0;
		int ownershipGainedCount = 0;
		int ownershipLostCount = 0;
		List<Exception> handlerExceptions = [];

		client.Connect(transport);
		client.EventHandlerException += _ => throw new InvalidOperationException("Exception handler failed.");
		client.EventHandlerException += handlerExceptions.Add;
		client.EntityObserved += _ => throw new InvalidOperationException("Observed handler failed.");
		client.EntityObserved += _ => observedCount++;
		client.EntityUnobserved += _ => throw new InvalidOperationException("Unobserved handler failed.");
		client.EntityUnobserved += _ => unobservedCount++;
		client.EntityOwnershipGained += _ => throw new InvalidOperationException("Ownership gained handler failed.");
		client.EntityOwnershipGained += _ => ownershipGainedCount++;
		client.EntityOwnershipLost += _ => throw new InvalidOperationException("Ownership lost handler failed.");
		client.EntityOwnershipLost += _ => ownershipLostCount++;

		Assert.That(() => client.Spawn(entity), Throws.Nothing);
		client.Tick();
		client.AssignOwner(entity, Guid.NewGuid());
		Assert.That(() => client.Tick(), Throws.Nothing);
		Assert.That(() => client.Receive(new MemoryRelayTransport(), BuildAssignOwnerMessage(entity.Id)), Throws.Nothing);
		client.Delete(entity);
		Assert.That(() => client.Tick(), Throws.Nothing);

		Assert.Multiple(() => {
			Assert.That(observedCount, Is.EqualTo(1));
			Assert.That(unobservedCount, Is.EqualTo(1));
			Assert.That(ownershipGainedCount, Is.EqualTo(2));
			Assert.That(ownershipLostCount, Is.EqualTo(1));
			Assert.That(handlerExceptions.Select(static exception => exception.Message), Is.EqualTo(new[] {
				"Observed handler failed.",
				"Ownership gained handler failed.",
				"Ownership lost handler failed.",
				"Ownership gained handler failed.",
				"Unobserved handler failed."
			}));
		});
	}

	[Test]
	public void RelayClientEntityLifecycleEvents_RemoveUnsubscribesHandler() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayValueState));
		RelayClient client = new(catalogue);
		int observedCount = 0;

		void OnObserved(NetworkEntity _) {
			observedCount++;
		}

		client.EntityObserved += OnObserved;
		client.EntityObserved -= OnObserved;
		client.Spawn(new RelayValueState { Value = 3 });

		Assert.That(observedCount, Is.EqualTo(0));
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
	public void RelayClientOwnedCollectionIndexReplacement_SendsDirtyStateToOtherClients() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayValueCollectionState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		TestEntityStorage serverStorage = new();
		RelayServer server = new(daemon, catalogue, serverStorage);
		RelayClient firstClient = new(catalogue);
		RelayClient secondClient = new(catalogue);

		firstClient.Connect(daemon.Connect());
		secondClient.Connect(daemon.Connect());
		Pump(server, firstClient, secondClient);

		RelayValueCollectionState entity = new();
		entity.Values.Add(3);
		entity.Values.Add(5);
		firstClient.Spawn(entity);
		Pump(server, firstClient, secondClient);

		entity.Values[1] = 9;
		Pump(server, firstClient, secondClient);

		Assert.Multiple(() => {
			Assert.That(serverStorage.TryGetEntity(entity.Id, out NetworkEntity? serverEntity), Is.True);
			Assert.That(secondClient.TryGetEntity(entity.Id, out NetworkEntity? secondClientEntity), Is.True);

			RelayValueCollectionState serverCollectionEntity = (RelayValueCollectionState)serverEntity!;
			RelayValueCollectionState secondClientCollectionEntity = (RelayValueCollectionState)secondClientEntity!;
			Assert.That(serverCollectionEntity.Values.Count, Is.EqualTo(2));
			Assert.That(serverCollectionEntity.Values[0], Is.EqualTo(3));
			Assert.That(serverCollectionEntity.Values[1], Is.EqualTo(9));
			Assert.That(secondClientCollectionEntity.Values.Count, Is.EqualTo(2));
			Assert.That(secondClientCollectionEntity.Values[0], Is.EqualTo(3));
			Assert.That(secondClientCollectionEntity.Values[1], Is.EqualTo(9));
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
	public void RelayServer_SerializesRelevantEntityWithNullStringProperty() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RelayProfileState), typeof(RelayStringState));
		MemoryRelayDaemon daemon = new(() => new RelayProfileState());
		TestEntityStorage serverStorage = new();
		RelayStringState entity = new() {
			Path = null!
		};
		serverStorage.RegisterEntity(entity);
		RelayServer server = new(daemon, catalogue, serverStorage);
		RelayClient client = new(catalogue);

		client.Connect(daemon.Connect());
		Assert.That(() => Pump(server, client), Throws.Nothing);

		Assert.Multiple(() => {
			Assert.That(client.TryGetEntity(entity.Id, out NetworkEntity? clientEntity), Is.True);
			Assert.That(clientEntity, Is.TypeOf<RelayStringState>());
			Assert.That(((RelayStringState)clientEntity!).Path, Is.Null);
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
			[(byte)ProfileMessageKind.Create],
			GuidBytes(profileId),
			GuidBytes(typeId),
			Int32(payload.Length),
			payload);
	}

	private static byte[] BuildAssignProfileMessage(Guid profileId) {
		return Concat(
			[(byte)NetworkMessageChannel.ProfileMessage],
			[(byte)ProfileMessageKind.Assign],
			GuidBytes(profileId));
	}

	private static byte[] BuildDeleteProfileMessage(Guid profileId) {
		return Concat(
			[(byte)NetworkMessageChannel.ProfileMessage],
			[(byte)ProfileMessageKind.Delete],
			GuidBytes(profileId));
	}

	private static byte[] BuildCreateEntityMessage(TypeCatalogue catalogue, Guid entityId, NetworkEntity entity) {
		byte[] payload = Serialize(entity, catalogue);
		Assert.That(catalogue.TryFindTypeId(entity.GetType(), out Guid typeId), Is.True);
		return Concat(
			[(byte)NetworkMessageChannel.EntityMessage],
			[(byte)EntityMessageKind.Create],
			GuidBytes(entityId),
			GuidBytes(typeId),
			Int32(payload.Length),
			payload);
	}

	private static byte[] BuildAssignOwnerMessage(Guid entityId) {
		return Concat(
			[(byte)NetworkMessageChannel.EntityMessage],
			[(byte)EntityMessageKind.AssignOwner],
			GuidBytes(entityId));
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

	private sealed class ExposedRelayServer(IDaemon daemon, TypeCatalogue typeCatalogue, EntityStorage entityStorage)
		: RelayServer(daemon, typeCatalogue, entityStorage) {

		public void Receive(IRelayTransport sender, ReadOnlySpan<byte> message) {
			ProcessMessage(sender, message);
		}
	}

	private sealed class LoadingEntityStorage : TestEntityStorage {
		public bool LoadEntity(Guid id, NetworkEntity entity) {
			entity.AssignNetworkId(id);
			return RegisterEntity(entity);
		}

		public void SetNetworkId(NetworkEntity entity, Guid id) {
			entity.AssignNetworkId(id);
		}
	}

	private sealed class RecordingRelayTransport : IRelayTransport {
		public List<byte[]> SentMessages { get; } = [];

		public event MessageHandler? MessageReceived;
		public event Action<IRelayTransport>? Disconnected;

		public void Send(ReadOnlySpan<byte> message) {
			SentMessages.Add(message.ToArray());
		}

		public void PumpMessages() {
		}

		public void Receive(ReadOnlySpan<byte> message) {
			MessageReceived?.Invoke(this, message);
		}

		public void Disconnect() {
			Disconnected?.Invoke(this);
		}
	}

	private sealed class ThrowingRelayTransport : IRelayTransport {
		public List<byte[]> SentMessages { get; } = [];
		public bool ThrowOnSend { get; set; }
		public bool ThrowOnPump { get; set; }

		public event MessageHandler? MessageReceived;
		public event Action<IRelayTransport>? Disconnected;

		public void Send(ReadOnlySpan<byte> message) {
			if (ThrowOnSend) {
				throw new IOException("Transport send failed.");
			}

			SentMessages.Add(message.ToArray());
		}

		public void PumpMessages() {
			if (ThrowOnPump) {
				throw new IOException("Transport pump failed.");
			}
		}

		public void Receive(ReadOnlySpan<byte> message) {
			MessageReceived?.Invoke(this, message);
		}

		public void Disconnect() {
			Disconnected?.Invoke(this);
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


