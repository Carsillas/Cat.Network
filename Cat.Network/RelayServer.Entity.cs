namespace Cat.Network;

public partial class RelayServer {
	protected override void OnCreateEntityMessage(IRelayTransport sender, Guid entityId, Guid typeId, ReadOnlySpan<byte> data) {
		if (!ClientsByTransport.TryGetValue(sender, out RemoteClient? client) || EntityStorage.TryGetEntity(entityId, out _)) {
			return;
		}

		if (!TryCreateEntity(entityId, typeId, data, out NetworkEntity entity)) {
			return;
		}

		entity.Peer = this;
		EntityStorage.RegisterEntity(entity);
		client.KnownEntityIds.Add(entity.Id);
		SetOwner(entity.Id, client, ownerNotified: true);

		ClearDirtyState(entity);
	}

	protected override void OnUpdateEntityMessage(IRelayTransport sender, Guid entityId, ReadOnlySpan<byte> data) {
		if (!ClientsByTransport.TryGetValue(sender, out RemoteClient? client) ||
		    !EntityStorage.TryGetEntity(entityId, out NetworkEntity? entity) ||
		    !IsOwner(client, entityId)) {
			return;
		}

		if (!TryDeserializeEntityUpdate(entity, data)) {
			return;
		}
	}

	protected override void OnDeleteEntityMessage(IRelayTransport sender, Guid entityId) {
		if (!ClientsByTransport.TryGetValue(sender, out RemoteClient? client) ||
		    !EntityStorage.TryGetEntity(entityId, out NetworkEntity? entity) ||
		    !IsOwner(client, entityId)) {
			return;
		}

		SetOwner(entityId, null, ownerNotified: false);
		EntityStorage.UnregisterEntity(entityId);
		entity.Peer = null;
	}

	protected override void OnOwnershipTransferRequestMessage(IRelayTransport sender, Guid entityId, Guid ownerProfileId) {
		if (!ClientsByTransport.TryGetValue(sender, out RemoteClient? client) ||
		    !EntityStorage.TryGetEntity(entityId, out NetworkEntity? entity) ||
		    !IsOwner(client, entityId)) {
			return;
		}

		SetOwner(entityId, null, ownerNotified: false);
		if (ClientsByProfileId.TryGetValue(ownerProfileId, out RemoteClient? ownerClient) &&
		    ownerClient.KnownEntityIds.Contains(entityId)) {
			SetOwner(entityId, ownerClient, ownerNotified: false);
		}
	}

	private bool IsOwner(RemoteClient client, Guid entityId) {
		return OwnerProfileIdsByEntityId.TryGetValue(entityId, out Guid ownerProfileId) &&
		       ownerProfileId == client.Profile.Id;
	}

	private void ProcessEntityRelevancy() {
		DirtyEntityWorkingSet.Clear();

		foreach (RemoteClient client in Clients) {
			PopulateRelevantEntityIds(client);

			EntityWorkingBuffer.Clear();
			foreach (Guid knownEntityId in client.KnownEntityIds) {
				if (!RelevantEntityIdWorkingSet.Contains(knownEntityId)) {
					EntityWorkingBuffer.Add(knownEntityId);
				}
			}

			foreach (Guid knownEntityId in EntityWorkingBuffer) {
				MessageWriter.Clear();
				WriteDeleteEntityMessage(MessageWriter, knownEntityId);
				client.Transport.Send(MessageWriter.GetWrittenSpan());
				client.KnownEntityIds.Remove(knownEntityId);
				if (IsOwner(client, knownEntityId)) {
					SetOwner(knownEntityId, null, ownerNotified: false);
				}
			}

			EntityWorkingBuffer.Clear();

			foreach (Guid relevantEntityId in RelevantEntityIdWorkingSet) {
				if (!EntityStorage.TryGetEntity(relevantEntityId, out NetworkEntity? entity)) {
					continue;
				}

				if (!client.KnownEntityIds.Contains(relevantEntityId)) {
					MessageWriter.Clear();
					if (TryWriteCreateEntityMessage(MessageWriter, entity)) {
						client.Transport.Send(MessageWriter.GetWrittenSpan());
						client.KnownEntityIds.Add(entity.Id);
					} else {
						continue;
					}
				} else if (HasDirtyState(entity)) {
					MessageWriter.Clear();
					if (TryWriteUpdateEntityMessage(MessageWriter, entity)) {
						client.Transport.Send(MessageWriter.GetWrittenSpan());
						DirtyEntityWorkingSet.Add(entity);
					}
				}

				if (!OwnerProfileIdsByEntityId.ContainsKey(entity.Id)) {
					SetOwner(entity.Id, client, ownerNotified: false);
				}

				if (IsOwner(client, relevantEntityId) && client.OwnedEntityIds.Add(relevantEntityId)) {
					MessageWriter.Clear();
					WriteAssignOwnerMessage(MessageWriter, relevantEntityId);
					client.Transport.Send(MessageWriter.GetWrittenSpan());
				}
			}

			RelevantEntityIdWorkingSet.Clear();
		}

		foreach (NetworkEntity entity in DirtyEntityWorkingSet) {
			ClearDirtyState(entity);
		}

		DirtyEntityWorkingSet.Clear();
	}

	private void SetOwner(Guid entityId, RemoteClient? client, bool ownerNotified) {
		if (OwnerProfileIdsByEntityId.TryGetValue(entityId, out Guid ownerProfileId)) {
			if (client is not null && ownerProfileId == client.Profile.Id) {
				return;
			}

			if (ClientsByProfileId.TryGetValue(ownerProfileId, out RemoteClient? ownerClient)) {
				ownerClient.RemoveOwnership(entityId);
			}
		}

		if (client is null) {
			OwnerProfileIdsByEntityId.Remove(entityId);
			return;
		}

		OwnerProfileIdsByEntityId[entityId] = client.Profile.Id;
		if (ownerNotified) {
			client.OwnedEntityIds.Add(entityId);
		}
	}

	private void PopulateRelevantEntityIds(RemoteClient client) {
		RelevantEntityWorkingBuffer.Clear();
		RelevantEntityIdWorkingSet.Clear();
		EntityStorage.PopulateRelevantEntities(client.Profile, RelevantEntityWorkingBuffer);

		foreach (NetworkEntity entity in RelevantEntityWorkingBuffer) {
			RelevantEntityIdWorkingSet.Add(entity.Id);
		}

		RelevantEntityWorkingBuffer.Clear();
	}

}
