namespace Cat.Network;

public partial class RelayClient {
	public void Spawn(NetworkEntity entity) {
		ArgumentNullException.ThrowIfNull(entity);
		long sessionVersion = SessionVersion;
		if (entity.Id == Guid.Empty) {
			entity.Id = Guid.NewGuid();
		}

		bool ownershipGained = OwnedEntityIds.Add(entity.Id);
		RegisterEntity(entity);
		if (SessionVersion != sessionVersion) {
			return;
		}
		if (ownershipGained) {
			RaiseEntityEvent(EntityOwnershipGainedHandlers, entity);
		}

		if (SessionVersion == sessionVersion) {
			EntitiesToSpawn.Add(entity);
		}
	}

	public void Delete(NetworkEntity entity) {
		ArgumentNullException.ThrowIfNull(entity);
		if (!Owns(entity)) {
			throw new InvalidOperationException("Cannot delete a network entity this client does not own.");
		}

		if (EntitiesToSpawn.Remove(entity)) {
			OwnershipTransferRequests.RemoveAll(request => request.EntityId == entity.Id);
			UnregisterEntity(entity);
			return;
		}

		OwnershipTransferRequests.RemoveAll(request => request.EntityId == entity.Id);
		EntitiesToDelete.Add(entity);
	}

	public void AssignOwner(NetworkEntity entity, Guid newOwnerProfileId) {
		ArgumentNullException.ThrowIfNull(entity);
		if (!Owns(entity)) {
			throw new InvalidOperationException("Cannot transfer ownership for a network entity this client does not own.");
		}

		OwnershipTransferRequests.RemoveAll(request => request.EntityId == entity.Id);
		OwnershipTransferRequests.Add(new OwnershipTransferRequest(entity.Id, newOwnerProfileId));
	}

	protected override void OnAssignOwnerMessage(IRelayTransport sender, Guid entityId) {
		if (!TryGetEntity(entityId, out NetworkEntity? entity)) {
			return;
		}

		AddOwnership(entity);
	}

	protected override void OnCreateEntityMessage(IRelayTransport sender, Guid entityId, Guid typeId, ReadOnlySpan<byte> data) {
		long sessionVersion = SessionVersion;
		if (TryGetEntity(entityId, out NetworkEntity? existingEntity)) {
			existingEntity.Peer = this;
			return;
		}

		if (!TryCreateEntity(entityId, typeId, data, out NetworkEntity entity) || SessionVersion != sessionVersion) {
			return;
		}

		RegisterEntity(entity);
		if (SessionVersion == sessionVersion) {
			ClearDirtyState(entity);
		}
	}

	protected override void OnUpdateEntityMessage(IRelayTransport sender, Guid entityId, ReadOnlySpan<byte> data) {
		long sessionVersion = SessionVersion;
		if (!TryGetEntity(entityId, out NetworkEntity? entity) || Owns(entity)) {
			return;
		}

		if (!TryDeserializeEntityUpdate(entity, data) || SessionVersion != sessionVersion) {
			return;
		}

		ClearDirtyState(entity);
	}

	protected override void OnDeleteEntityMessage(IRelayTransport sender, Guid entityId) {
		if (!TryGetEntity(entityId, out NetworkEntity? entity)) {
			return;
		}

		UnregisterEntity(entity);
	}

	protected override void OnRpcMessage(IRelayTransport sender, Guid entityId, ReadOnlySpan<byte> data) {
		InvokeReceivedMessage(entityId, data, rpc: true);
	}

	protected override void OnBroadcastMessage(IRelayTransport sender, Guid entityId, ReadOnlySpan<byte> data) {
		InvokeReceivedMessage(entityId, data, rpc: false);
	}

	private void ProcessOutgoingMessages(IRelayTransport transport, long sessionVersion) {
		foreach (NetworkEntity entity in Entities) {
			if (EntitiesToSpawn.Contains(entity) || EntitiesToDelete.Contains(entity) || !Owns(entity) || !HasDirtyState(entity)) {
				continue;
			}

			MessageWriter.Clear();
			if (TryWriteUpdateEntityMessage(MessageWriter, entity)) {
				if (!TrySendSessionMessage(transport, sessionVersion, MessageWriter.GetWrittenSpan())) {
					return;
				}
				ClearDirtyState(entity);
			}
			if (!IsCurrentSession(transport, sessionVersion)) {
				return;
			}
		}

		foreach (NetworkEntity entity in EntitiesToSpawn) {
			MessageWriter.Clear();
			if (TryWriteCreateEntityMessage(MessageWriter, entity)) {
				if (!TrySendSessionMessage(transport, sessionVersion, MessageWriter.GetWrittenSpan())) {
					return;
				}
				ClearDirtyState(entity);
			}
			if (!IsCurrentSession(transport, sessionVersion)) {
				return;
			}
		}

		EntitiesToSpawn.Clear();

		foreach (NetworkEntity entity in EntitiesToDelete) {
			MessageWriter.Clear();
			WriteDeleteEntityMessage(MessageWriter, entity.Id);
			if (!TrySendSessionMessage(transport, sessionVersion, MessageWriter.GetWrittenSpan())) {
				return;
			}
			UnregisterEntity(entity);
			if (!IsCurrentSession(transport, sessionVersion)) {
				return;
			}
		}

		EntitiesToDelete.Clear();

		while (OutgoingMessageWriters.TryDequeue(out BufferWriter? writer)) {
			try {
				if (!TrySendSessionMessage(transport, sessionVersion, writer.GetWrittenSpan())) {
					return;
				}
			} finally {
				ReturnMessageWriter(writer);
			}
		}

		foreach (OwnershipTransferRequest request in OwnershipTransferRequests) {
			MessageWriter.Clear();
			WriteOwnershipTransferRequestMessage(MessageWriter, request.EntityId, request.NewOwnerProfileId);
			if (!TrySendSessionMessage(transport, sessionVersion, MessageWriter.GetWrittenSpan())) {
				return;
			}
			if (TryGetEntity(request.EntityId, out NetworkEntity? entity)) {
				RemoveOwnership(entity);
			}
			if (!IsCurrentSession(transport, sessionVersion)) {
				return;
			}
		}

		OwnershipTransferRequests.Clear();
	}

	private bool RegisterEntity(NetworkEntity entity) {
		long sessionVersion = SessionVersion;
		if (EntitiesById.TryGetValue(entity.Id, out NetworkEntity? existingEntity) &&
		    !ReferenceEquals(existingEntity, entity)) {
			UnregisterEntity(existingEntity);
			if (SessionVersion != sessionVersion) {
				return false;
			}
		} else if (existingEntity is not null) {
			entity.Peer = this;
			return false;
		}

		Entities.Add(entity);
		EntitiesById[entity.Id] = entity;
		entity.Peer = this;
		RaiseEntityEvent(EntityObservedHandlers, entity);
		return true;
	}

	private bool UnregisterEntity(NetworkEntity entity) {
		if (!EntitiesById.TryGetValue(entity.Id, out NetworkEntity? existingEntity) ||
		    !ReferenceEquals(existingEntity, entity)) {
			return false;
		}

		Entities.Remove(entity);
		EntitiesById.Remove(entity.Id);
		OwnedEntityIds.Remove(entity.Id);
		entity.Peer = null;
		RaiseEntityEvent(EntityUnobservedHandlers, entity);
		return true;
	}

	private bool AddOwnership(NetworkEntity entity) {
		if (!OwnedEntityIds.Add(entity.Id)) {
			return false;
		}

		entity.Peer = this;
		RaiseEntityEvent(EntityOwnershipGainedHandlers, entity);
		return true;
	}

	private bool RemoveOwnership(NetworkEntity entity) {
		if (!OwnedEntityIds.Remove(entity.Id)) {
			return false;
		}

		RaiseEntityEvent(EntityOwnershipLostHandlers, entity);
		return true;
	}

	private readonly record struct OwnershipTransferRequest(Guid EntityId, Guid NewOwnerProfileId);

	private void InvokeReceivedMessage(Guid entityId, ReadOnlySpan<byte> data, bool rpc) {
		if (!TryGetEntity(entityId, out NetworkEntity? entity) ||
		    !data.TryConsumeGuid(out Guid instigatorProfileId) ||
		    !TryGetProfile(instigatorProfileId, out NetworkProfile? instigator) ||
		    !data.TryConsumeUInt64(out ulong messageId)) {
			return;
		}

		INetworkRpcTarget rpcTarget = entity;
		SerializationContext context = new(TypeCatalogue);
		if (rpc) {
			rpcTarget.TryInvokeRpc(this, instigator, messageId, data, context);
		} else {
			rpcTarget.TryInvokeBroadcast(this, instigator, messageId, data, context);
		}
	}
}
