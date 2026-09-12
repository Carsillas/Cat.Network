namespace Cat.Network;

public partial class RelayClient {
	public void Spawn(NetworkEntity entity) {
		ArgumentNullException.ThrowIfNull(entity);
		if (entity.Id == Guid.Empty) {
			entity.Id = Guid.NewGuid();
		}

		bool ownershipGained = OwnedEntityIds.Add(entity.Id);
		RegisterEntity(entity);
		if (ownershipGained) {
			RaiseEntityEvent(EntityOwnershipGainedHandlers, entity);
		}

		EntitiesToSpawn.Add(entity);
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
		if (TryGetEntity(entityId, out NetworkEntity? existingEntity)) {
			existingEntity.Peer = this;
			return;
		}

		if (!TryCreateEntity(entityId, typeId, data, out NetworkEntity entity)) {
			return;
		}

		RegisterEntity(entity);
		ClearDirtyState(entity);
	}

	protected override void OnUpdateEntityMessage(IRelayTransport sender, Guid entityId, ReadOnlySpan<byte> data) {
		if (!TryGetEntity(entityId, out NetworkEntity? entity) || Owns(entity)) {
			return;
		}

		if (!TryDeserializeEntityUpdate(entity, data)) {
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

	private void ProcessOutgoingMessages(IRelayTransport transport) {
		// Outgoing lifecycle callbacks can queue more work. Defer it to the next tick.
		NetworkEntity[] deleteBatch = EntitiesToDelete.ToArray();
		OwnershipTransferRequest[] ownershipTransferBatch = OwnershipTransferRequests.ToArray();

		foreach (NetworkEntity entity in Entities) {
			if (EntitiesToSpawn.Contains(entity) || EntitiesToDelete.Contains(entity) || !Owns(entity) || !HasDirtyState(entity)) {
				continue;
			}

			MessageWriter.Clear();
			if (TryWriteUpdateEntityMessage(MessageWriter, entity)) {
				transport.Send(MessageWriter.GetWrittenSpan());
				ClearDirtyState(entity);
			}
		}

		foreach (NetworkEntity entity in EntitiesToSpawn) {
			MessageWriter.Clear();
			if (TryWriteCreateEntityMessage(MessageWriter, entity)) {
				transport.Send(MessageWriter.GetWrittenSpan());
				ClearDirtyState(entity);
			}
		}

		EntitiesToSpawn.Clear();

		foreach (NetworkEntity entity in deleteBatch) {
			if (!EntitiesToDelete.Contains(entity)) {
				continue;
			}

			MessageWriter.Clear();
			WriteDeleteEntityMessage(MessageWriter, entity.Id);
			transport.Send(MessageWriter.GetWrittenSpan());
			EntitiesToDelete.Remove(entity);
			UnregisterEntity(entity);
		}

		while (OutgoingMessageWriters.TryDequeue(out BufferWriter? writer)) {
			transport.Send(writer.GetWrittenSpan());
			ReturnMessageWriter(writer);
		}

		foreach (OwnershipTransferRequest request in ownershipTransferBatch) {
			// An earlier callback may have canceled or replaced this exact request.
			if (!OwnershipTransferRequests.Contains(request)) {
				continue;
			}

			MessageWriter.Clear();
			WriteOwnershipTransferRequestMessage(MessageWriter, request.EntityId, request.NewOwnerProfileId);
			transport.Send(MessageWriter.GetWrittenSpan());
			OwnershipTransferRequests.Remove(request);
			if (TryGetEntity(request.EntityId, out NetworkEntity? entity)) {
				RemoveOwnership(entity);
			}
		}
	}

	private bool RegisterEntity(NetworkEntity entity) {
		if (EntitiesById.TryGetValue(entity.Id, out NetworkEntity? existingEntity) &&
		    !ReferenceEquals(existingEntity, entity)) {
			UnregisterEntity(existingEntity);
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

	// Replacing a request must invalidate its batch entry even when both ids are unchanged.
	private sealed class OwnershipTransferRequest(Guid entityId, Guid newOwnerProfileId) {
		public Guid EntityId { get; } = entityId;
		public Guid NewOwnerProfileId { get; } = newOwnerProfileId;
	}

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
