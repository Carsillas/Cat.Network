namespace Cat.Network;

public partial class RelayServer {
	protected override void OnCreateEntityMessage(IRelayTransport sender, Guid entityId, Guid typeId, ReadOnlySpan<byte> data) {
		if (entityId == Guid.Empty) {
			throw new InvalidOperationException("Client-created network entities must provide a non-empty network id.");
		}

		if (!ClientsByTransport.TryGetValue(sender, out RemoteClient? client) || EntityStorage.TryGetEntity(entityId, out _)) {
			return;
		}

		if (!TryCreateEntity(entityId, typeId, data, out NetworkEntity entity)) {
			return;
		}

		if (!EntityStorage.RegisterEntity(entity)) {
			return;
		}

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

	protected override void OnRpcMessage(IRelayTransport sender, Guid entityId, ReadOnlySpan<byte> data) {
		if (!ClientsByTransport.TryGetValue(sender, out RemoteClient? client) ||
		    !client.KnownEntityIds.Contains(entityId) ||
		    !EntityStorage.TryGetEntity(entityId, out _) ||
		    IsOwner(client, entityId) ||
		    !OwnerProfileIdsByEntityId.TryGetValue(entityId, out Guid ownerProfileId) ||
		    !ClientsByProfileId.TryGetValue(ownerProfileId, out RemoteClient? ownerClient) ||
		    !ownerClient.KnownEntityIds.Contains(entityId)) {
			return;
		}

		MessageWriter.Clear();
		WriteForwardedRpcMessage(MessageWriter, entityId, client.Profile.Id, data);
		TrySend(ownerClient, MessageWriter.GetWrittenSpan());
	}

	protected override void OnBroadcastMessage(IRelayTransport sender, Guid entityId, ReadOnlySpan<byte> data) {
		if (!ClientsByTransport.TryGetValue(sender, out RemoteClient? ownerClient) ||
		    !EntityStorage.TryGetEntity(entityId, out _) ||
		    !IsOwner(ownerClient, entityId)) {
			return;
		}

		MessageWriter.Clear();
		WriteForwardedBroadcastMessage(MessageWriter, entityId, ownerClient.Profile.Id, data);
		ReadOnlySpan<byte> message = MessageWriter.GetWrittenSpan();
		foreach (RemoteClient client in Clients) {
			if (ReferenceEquals(client, ownerClient) || !client.KnownEntityIds.Contains(entityId)) {
				continue;
			}

			TrySend(client, message);
		}
	}

	private static void WriteForwardedRpcMessage(BufferWriter writer, Guid entityId, Guid instigatorProfileId, ReadOnlySpan<byte> data) {
		WriteForwardedMessage(writer, EntityMessageKind.Rpc, entityId, instigatorProfileId, data);
	}

	private static void WriteForwardedBroadcastMessage(BufferWriter writer, Guid entityId, Guid instigatorProfileId, ReadOnlySpan<byte> data) {
		WriteForwardedMessage(writer, EntityMessageKind.Broadcast, entityId, instigatorProfileId, data);
	}

	private static void WriteForwardedMessage(BufferWriter writer, EntityMessageKind kind, Guid entityId, Guid instigatorProfileId, ReadOnlySpan<byte> data) {
		WriteEntityMessageHeader(writer, kind, entityId);
		Range lengthRange = writer.Reserve(sizeof(int));
		int dataStart = writer.WrittenCount;
		writer.WriteGuid(instigatorProfileId);
		Span<byte> payload = writer.GetSpan(data.Length);
		data.CopyTo(payload);
		writer.Advance(data.Length);
		writer.WriteInt32(lengthRange, writer.WrittenCount - dataStart);
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
				if (!TrySend(client, MessageWriter.GetWrittenSpan())) {
					continue;
				}

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
						if (!TrySend(client, MessageWriter.GetWrittenSpan())) {
							continue;
						}

						client.KnownEntityIds.Add(entity.Id);
					} else {
						continue;
					}
				} else if (HasDirtyState(entity)) {
					MessageWriter.Clear();
					if (TryWriteUpdateEntityMessage(MessageWriter, entity)) {
						if (!TrySend(client, MessageWriter.GetWrittenSpan())) {
							continue;
						}

						DirtyEntityWorkingSet.Add(entity);
					}
				}

				if (!OwnerProfileIdsByEntityId.ContainsKey(entity.Id)) {
					SetOwner(entity.Id, client, ownerNotified: false);
				}

				if (IsOwner(client, relevantEntityId) && client.OwnedEntityIds.Add(relevantEntityId)) {
					MessageWriter.Clear();
					WriteAssignOwnerMessage(MessageWriter, relevantEntityId);
					TrySend(client, MessageWriter.GetWrittenSpan());
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
