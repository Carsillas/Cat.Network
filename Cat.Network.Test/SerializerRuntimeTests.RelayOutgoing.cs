using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	[TestCase(false)]
	[TestCase(true)]
	public void RelayClientOutgoing_UnobservedCallbackDeletesOnLaterTicks(bool callbackThrows) {
		RecordingRelayTransport transport = new();
		(RelayClient client, RelayValueState[] entities) = CreateOutgoingCallbackClient(transport, 1, 2, 3);
		transport.SentMessages.Clear();
		List<NetworkEntity> unobserved = [];
		List<Exception> errors = [];
		InvalidOperationException failure = new("Delete callback failed after queuing work.");
		client.EventHandlerException += _ => throw new InvalidOperationException("Exception handler failed.");
		client.EventHandlerException += errors.Add;
		client.EntityUnobserved += entity => {
			if (ReferenceEquals(entity, entities[0])) {
				client.Delete(entities[1]);
				if (callbackThrows) {
					throw failure;
				}
			} else if (ReferenceEquals(entity, entities[1])) {
				client.Delete(entities[2]);
			}
		};
		client.EntityUnobserved += unobserved.Add;
		client.Delete(entities[0]);

		for (int tick = 0; tick < entities.Length; tick++) {
			Assert.That(client.Tick, Throws.Nothing);
			Assert.That(transport.SentMessages, Is.EqualTo(entities.Take(tick + 1)
				.Select(entity => BuildDeleteEntityMessage(entity.Id)).ToArray()));
			Assert.That(unobserved, Has.Count.EqualTo(tick + 1));
			Assert.That(unobserved[tick], Is.SameAs(entities[tick]));
			for (int index = 0; index < entities.Length; index++) {
				bool pending = index > tick;
				Assert.That(client.TryGetEntity(entities[index].Id, out NetworkEntity? registered), Is.EqualTo(pending));
				Assert.That(entities[index].IsOwner, Is.EqualTo(pending));
				if (pending) {
					Assert.That(registered, Is.SameAs(entities[index]));
				} else {
					Assert.That(entities[index].Peer, Is.Null);
				}
			}
		}

		Assert.That(client.Tick, Throws.Nothing);
		Assert.That(transport.SentMessages, Has.Count.EqualTo(3));
		Assert.That(unobserved, Has.Count.EqualTo(3));
		Assert.That(errors, Has.Count.EqualTo(callbackThrows ? 1 : 0));
		if (callbackThrows) {
			Assert.That(errors[0], Is.SameAs(failure));
		}
	}

	[TestCase(false)]
	[TestCase(true)]
	public void RelayClientOutgoing_OwnershipLostCallbackTransfersOnLaterTicks(bool callbackThrows) {
		RecordingRelayTransport transport = new();
		(RelayClient client, RelayValueState[] entities) = CreateOutgoingCallbackClient(transport, 1, 2, 3);
		transport.SentMessages.Clear();
		Guid newOwner = Guid.NewGuid();
		List<NetworkEntity> lostOwnership = [];
		List<Exception> errors = [];
		InvalidOperationException failure = new("Ownership callback failed after queuing work.");
		client.EventHandlerException += _ => throw new InvalidOperationException("Exception handler failed.");
		client.EventHandlerException += errors.Add;
		client.EntityOwnershipLost += entity => {
			if (ReferenceEquals(entity, entities[0])) {
				client.AssignOwner(entities[1], newOwner);
				if (callbackThrows) {
					throw failure;
				}
			} else if (ReferenceEquals(entity, entities[1])) {
				client.AssignOwner(entities[2], newOwner);
			}
		};
		client.EntityOwnershipLost += lostOwnership.Add;
		client.AssignOwner(entities[0], newOwner);

		for (int tick = 0; tick < entities.Length; tick++) {
			Assert.That(client.Tick, Throws.Nothing);
			Assert.That(transport.SentMessages, Is.EqualTo(entities.Take(tick + 1)
				.Select(entity => BuildOwnershipTransferRequestMessage(entity.Id, newOwner)).ToArray()));
			Assert.That(lostOwnership, Has.Count.EqualTo(tick + 1));
			Assert.That(lostOwnership[tick], Is.SameAs(entities[tick]));
			for (int index = 0; index < entities.Length; index++) {
				Assert.That(entities[index].IsOwner, Is.EqualTo(index > tick));
				Assert.That(client.TryGetEntity(entities[index].Id, out NetworkEntity? registered), Is.True);
				Assert.That(registered, Is.SameAs(entities[index]));
			}
		}

		Assert.That(client.Tick, Throws.Nothing);
		Assert.That(transport.SentMessages, Has.Count.EqualTo(3));
		Assert.That(lostOwnership, Has.Count.EqualTo(3));
		Assert.That(errors, Has.Count.EqualTo(callbackThrows ? 1 : 0));
		if (callbackThrows) {
			Assert.That(errors[0], Is.SameAs(failure));
		}
	}

	[TestCase(false, false)]
	[TestCase(false, true)]
	[TestCase(true, false)]
	[TestCase(true, true)]
	public void RelayClientOutgoing_CallbackReplacementDefersTheNewTransfer(bool fromDelete, bool sameOwner) {
		RecordingRelayTransport transport = new();
		(RelayClient client, RelayValueState[] entities) = CreateOutgoingCallbackClient(transport, 1, 2, 3);
		transport.SentMessages.Clear();
		Guid originalOwner = Guid.NewGuid();
		Guid replacementOwner = sameOwner ? originalOwner : Guid.NewGuid();
		List<NetworkEntity> lostOwnership = [];
		List<Exception> errors = [];
		client.EventHandlerException += errors.Add;
		Action<NetworkEntity> replacePendingTransfer = entity => {
			if (ReferenceEquals(entity, entities[0])) {
				client.AssignOwner(entities[1], replacementOwner);
			}
		};
		if (fromDelete) {
			client.EntityUnobserved += replacePendingTransfer;
			client.Delete(entities[0]);
		} else {
			client.EntityOwnershipLost += replacePendingTransfer;
			client.AssignOwner(entities[0], originalOwner);
		}

		client.EntityOwnershipLost += lostOwnership.Add;
		client.AssignOwner(entities[1], originalOwner);
		client.AssignOwner(entities[2], originalOwner);
		byte[] firstMessage = fromDelete
			? BuildDeleteEntityMessage(entities[0].Id)
			: BuildOwnershipTransferRequestMessage(entities[0].Id, originalOwner);
		byte[] thirdMessage = BuildOwnershipTransferRequestMessage(entities[2].Id, originalOwner);
		Assert.That(client.Tick, Throws.Nothing);

		Assert.That(transport.SentMessages, Is.EqualTo(new[] { firstMessage, thirdMessage }));
		Assert.That(entities[1].IsOwner, Is.True);
		Assert.That(entities[2].IsOwner, Is.False);
		Assert.That(lostOwnership, Has.Count.EqualTo(fromDelete ? 1 : 2));
		Assert.That(lostOwnership[^1], Is.SameAs(entities[2]));
		if (!fromDelete) {
			Assert.That(lostOwnership[0], Is.SameAs(entities[0]));
		}

		Assert.That(client.Tick, Throws.Nothing);
		Assert.That(client.Tick, Throws.Nothing);
		Assert.That(transport.SentMessages, Is.EqualTo(new[] {
			firstMessage, thirdMessage, BuildOwnershipTransferRequestMessage(entities[1].Id, replacementOwner)
		}));
		Assert.That(entities[1].IsOwner, Is.False);
		Assert.That(lostOwnership, Has.Count.EqualTo(fromDelete ? 2 : 3));
		Assert.That(lostOwnership[^1], Is.SameAs(entities[1]));
		Assert.That(errors, Is.Empty);
	}

	[TestCase(false)]
	[TestCase(true)]
	public void RelayClientOutgoing_CallbackDeleteCancelsPendingTransfer(bool fromDelete) {
		RecordingRelayTransport transport = new();
		(RelayClient client, RelayValueState[] entities) = CreateOutgoingCallbackClient(transport, 1, 2);
		transport.SentMessages.Clear();
		Guid newOwner = Guid.NewGuid();
		List<NetworkEntity> unobserved = [];
		List<NetworkEntity> lostOwnership = [];
		List<Exception> errors = [];
		client.EventHandlerException += errors.Add;
		Action<NetworkEntity> deletePendingTransfer = entity => {
			if (ReferenceEquals(entity, entities[0])) {
				client.Delete(entities[1]);
			}
		};
		if (fromDelete) {
			client.EntityUnobserved += deletePendingTransfer;
			client.Delete(entities[0]);
		} else {
			client.EntityOwnershipLost += deletePendingTransfer;
			client.AssignOwner(entities[0], newOwner);
		}

		client.EntityUnobserved += unobserved.Add;
		client.EntityOwnershipLost += lostOwnership.Add;
		client.AssignOwner(entities[1], newOwner);
		byte[] firstMessage = fromDelete
			? BuildDeleteEntityMessage(entities[0].Id)
			: BuildOwnershipTransferRequestMessage(entities[0].Id, newOwner);
		Assert.That(client.Tick, Throws.Nothing);

		Assert.That(transport.SentMessages, Is.EqualTo(new[] { firstMessage }));
		Assert.That(entities[1].IsOwner, Is.True);
		Assert.That(client.TryGetEntity(entities[1].Id, out NetworkEntity? pendingEntity), Is.True);
		Assert.That(pendingEntity, Is.SameAs(entities[1]));
		Assert.That(client.Tick, Throws.Nothing);
		Assert.That(client.Tick, Throws.Nothing);

		Assert.That(transport.SentMessages, Is.EqualTo(new[] { firstMessage, BuildDeleteEntityMessage(entities[1].Id) }));
		Assert.That(client.TryGetEntity(entities[1].Id, out _), Is.False);
		Assert.That(entities[1].Peer, Is.Null);
		Assert.That(unobserved, Has.Count.EqualTo(fromDelete ? 2 : 1));
		Assert.That(unobserved[^1], Is.SameAs(entities[1]));
		Assert.That(lostOwnership, Has.Count.EqualTo(fromDelete ? 0 : 1));
		if (!fromDelete) {
			Assert.That(lostOwnership[0], Is.SameAs(entities[0]));
		}
		Assert.That(errors, Is.Empty);
	}

	[Test]
	public void RelayClientOutgoing_UnobservedCallbackDefersNewTransferWhileSendingExistingTransfer() {
		RecordingRelayTransport transport = new();
		(RelayClient client, RelayValueState[] entities) = CreateOutgoingCallbackClient(transport, 1, 2, 3);
		transport.SentMessages.Clear();
		Guid newOwner = Guid.NewGuid();
		List<NetworkEntity> completed = [];
		client.EntityUnobserved += entity => {
			if (ReferenceEquals(entity, entities[0])) {
				client.AssignOwner(entities[1], newOwner);
			}
		};
		client.EntityUnobserved += completed.Add;
		client.EntityOwnershipLost += completed.Add;
		client.Delete(entities[0]);
		client.AssignOwner(entities[2], newOwner);
		byte[] deleteMessage = BuildDeleteEntityMessage(entities[0].Id);
		byte[] existingTransfer = BuildOwnershipTransferRequestMessage(entities[2].Id, newOwner);

		Assert.That(client.Tick, Throws.Nothing);
		Assert.That(transport.SentMessages, Is.EqualTo(new[] { deleteMessage, existingTransfer }));
		Assert.That(entities[1].IsOwner, Is.True);
		Assert.That(completed, Has.Count.EqualTo(2));
		Assert.That(completed[0], Is.SameAs(entities[0]));
		Assert.That(completed[1], Is.SameAs(entities[2]));

		Assert.That(client.Tick, Throws.Nothing);
		Assert.That(client.Tick, Throws.Nothing);
		Assert.That(transport.SentMessages, Is.EqualTo(new[] {
			deleteMessage, existingTransfer, BuildOwnershipTransferRequestMessage(entities[1].Id, newOwner)
		}));
		Assert.That(entities[1].IsOwner, Is.False);
		Assert.That(completed, Has.Count.EqualTo(3));
		Assert.That(completed[2], Is.SameAs(entities[1]));
	}

	[Test]
	public void RelayClientOutgoing_CallbackCanCancelAnUnsentSpawnAndItsTransfer() {
		RecordingRelayTransport transport = new();
		(RelayClient client, RelayValueState[] entities) = CreateOutgoingCallbackClient(transport, 1);
		transport.SentMessages.Clear();
		RelayValueState canceledEntity = new() { Value = 2 };
		Guid newOwner = Guid.NewGuid();
		List<NetworkEntity> unobserved = [];
		List<Exception> errors = [];
		client.EventHandlerException += errors.Add;
		client.EntityUnobserved += unobserved.Add;
		client.EntityOwnershipLost += entity => {
			if (ReferenceEquals(entity, entities[0])) {
				client.Spawn(canceledEntity);
				client.AssignOwner(canceledEntity, newOwner);
				client.Delete(canceledEntity);
			}
		};
		client.AssignOwner(entities[0], newOwner);

		Assert.That(client.Tick, Throws.Nothing);
		Assert.That(client.Tick, Throws.Nothing);
		Assert.That(transport.SentMessages, Is.EqualTo(new[] {
			BuildOwnershipTransferRequestMessage(entities[0].Id, newOwner)
		}));
		Assert.That(client.TryGetEntity(canceledEntity.Id, out _), Is.False);
		Assert.That(canceledEntity.Peer, Is.Null);
		Assert.That(unobserved, Has.Count.EqualTo(1));
		Assert.That(unobserved[0], Is.SameAs(canceledEntity));
		Assert.That(errors, Is.Empty);
	}

	[TestCase(false, false)]
	[TestCase(false, true)]
	[TestCase(true, false)]
	[TestCase(true, true)]
	public void RelayClientOutgoing_SendFailureRetainsFailedAndUnsentOperations(bool transfer, bool failBeforeAnySend) {
		ThrowingRelayTransport transport = new();
		(RelayClient client, RelayValueState[] entities) = CreateOutgoingCallbackClient(transport, 1, 2, 3);
		transport.SentMessages.Clear();
		Guid newOwner = Guid.NewGuid();
		List<NetworkEntity> completed = [];
		Action<NetworkEntity> onCompleted = entity => {
			completed.Add(entity);
			if (!failBeforeAnySend && completed.Count == 1) {
				transport.ThrowOnSend = true;
			}
		};
		if (transfer) {
			client.EntityOwnershipLost += onCompleted;
			foreach (RelayValueState entity in entities) {
				client.AssignOwner(entity, newOwner);
			}
		} else {
			client.EntityUnobserved += onCompleted;
			foreach (RelayValueState entity in entities) {
				client.Delete(entity);
			}
		}

		transport.ThrowOnSend = failBeforeAnySend;
		Assert.That(client.Tick, Throws.TypeOf<IOException>());
		Assert.That(transport.SentMessages, Has.Count.EqualTo(failBeforeAnySend ? 0 : 1));
		Assert.That(completed, Has.Count.EqualTo(transport.SentMessages.Count));
		foreach (RelayValueState entity in entities) {
			bool wasSent = completed.Any(item => ReferenceEquals(item, entity));
			Assert.That(entity.IsOwner, Is.EqualTo(!wasSent));
			Assert.That(entity.IsSpawned, Is.EqualTo(transfer || !wasSent));
		}

		transport.ThrowOnSend = false;
		Assert.That(client.Tick, Throws.Nothing);
		Assert.That(client.Tick, Throws.Nothing);
		byte[][] expectedMessages = entities.Select(entity => transfer
			? BuildOwnershipTransferRequestMessage(entity.Id, newOwner)
			: BuildDeleteEntityMessage(entity.Id)).ToArray();
		if (transfer) {
			Assert.That(transport.SentMessages, Is.EqualTo(expectedMessages));
		} else {
			Assert.That(transport.SentMessages, Is.EquivalentTo(expectedMessages));
		}
		Assert.That(completed, Has.Count.EqualTo(3));
		foreach (RelayValueState entity in entities) {
			Assert.That(completed.Count(item => ReferenceEquals(item, entity)), Is.EqualTo(1));
			Assert.That(entity.IsOwner, Is.False);
			Assert.That(entity.IsSpawned, Is.EqualTo(transfer));
		}
	}

	[TestCase(false)]
	[TestCase(true)]
	public void RelayClientOutgoing_DeleteUsesEntityIdentityAfterQueuing(bool mutateEntity) {
		RecordingRelayTransport transport = new();
		(RelayClient client, RelayValueState[] entities) = CreateOutgoingCallbackClient(transport, 1, 2);
		transport.SentMessages.Clear();
		List<NetworkEntity> unobserved = [];
		client.EntityUnobserved += unobserved.Add;
		client.Delete(entities[0]);
		if (mutateEntity) {
			entities[0].Value = 11;
		}

		Assert.That(client.Tick, Throws.Nothing);
		Assert.That(client.Tick, Throws.Nothing);
		Assert.That(transport.SentMessages, Is.EqualTo(new[] { BuildDeleteEntityMessage(entities[0].Id) }));
		Assert.That(unobserved, Has.Count.EqualTo(1));
		Assert.That(unobserved[0], Is.SameAs(entities[0]));
		Assert.That(client.TryGetEntity(entities[0].Id, out _), Is.False);
		Assert.That(entities[0].Peer, Is.Null);
		Assert.That(client.TryGetEntity(entities[1].Id, out NetworkEntity? survivor), Is.True);
		Assert.That(survivor, Is.SameAs(entities[1]));
		Assert.That(entities[1].IsOwner, Is.True);
	}

	[Test]
	public void RelayClientOutgoing_UnobservedCallbackCanMutateOtherQueuedDeletions() {
		RecordingRelayTransport transport = new();
		(RelayClient client, RelayValueState[] entities) = CreateOutgoingCallbackClient(transport, 1, 2, 3);
		transport.SentMessages.Clear();
		List<NetworkEntity> unobserved = [];
		client.EntityUnobserved += unobserved.Add;
		client.EntityUnobserved += _ => {
			if (unobserved.Count == 1) {
				foreach (RelayValueState pending in entities.Where(entity => entity.IsOwner)) {
					pending.Value += 10;
				}
			}
		};
		foreach (RelayValueState entity in entities) {
			client.Delete(entity);
		}

		Assert.That(client.Tick, Throws.Nothing);
		Assert.That(client.Tick, Throws.Nothing);
		Assert.That(transport.SentMessages, Is.EquivalentTo(entities.Select(entity => BuildDeleteEntityMessage(entity.Id)).ToArray()));
		Assert.That(unobserved, Has.Count.EqualTo(3));
		foreach (RelayValueState entity in entities) {
			Assert.That(unobserved.Count(item => ReferenceEquals(item, entity)), Is.EqualTo(1));
			Assert.That(client.TryGetEntity(entity.Id, out _), Is.False);
			Assert.That(entity.Peer, Is.Null);
		}
	}

	private static (RelayClient Client, RelayValueState[] Entities) CreateOutgoingCallbackClient(
		IRelayTransport transport, params int[] values) {
		RelayClient client = new(RegisterTypes(typeof(RelayValueState)));
		client.Connect(transport);
		RelayValueState[] entities = values.Select(value => new RelayValueState { Value = value }).ToArray();
		foreach (RelayValueState entity in entities) {
			client.Spawn(entity);
		}

		client.Tick();
		foreach (RelayValueState entity in entities) {
			Assert.That(client.TryGetEntity(entity.Id, out NetworkEntity? registered), Is.True);
			Assert.That(registered, Is.SameAs(entity));
			Assert.That(entity.IsOwner, Is.True);
		}

		return (client, entities);
	}
}
