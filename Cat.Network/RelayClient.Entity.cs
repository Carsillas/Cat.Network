namespace Cat.Network;

public partial class RelayClient {
	public void Spawn(NetworkEntity entity) {
		ArgumentNullException.ThrowIfNull(entity);
		if (entity.Id == Guid.Empty) {
			entity.Id = Guid.NewGuid();
		}

		entity.Peer = this;
		RegisterEntity(entity);
		OwnedEntityIds.Add(entity.Id);
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

		OwnedEntityIds.Add(entityId);
		entity.Peer = this;
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
	}

	protected override void OnUpdateEntityMessage(IRelayTransport sender, Guid entityId, ReadOnlySpan<byte> data) {
		if (!TryGetEntity(entityId, out NetworkEntity? entity) || Owns(entity)) {
			return;
		}

		TryDeserializeEntityUpdate(entity, data);
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

		foreach (NetworkEntity entity in EntitiesToDelete) {
			MessageWriter.Clear();
			WriteDeleteEntityMessage(MessageWriter, entity.Id);
			transport.Send(MessageWriter.GetWrittenSpan());
			UnregisterEntity(entity);
		}

		EntitiesToDelete.Clear();

		while (OutgoingMessageWriters.TryDequeue(out BufferWriter? writer)) {
			transport.Send(writer.GetWrittenSpan());
			ReturnMessageWriter(writer);
		}

		foreach (OwnershipTransferRequest request in OwnershipTransferRequests) {
			MessageWriter.Clear();
			WriteOwnershipTransferRequestMessage(MessageWriter, request.EntityId, request.NewOwnerProfileId);
			transport.Send(MessageWriter.GetWrittenSpan());
			OwnedEntityIds.Remove(request.EntityId);
		}

		OwnershipTransferRequests.Clear();
	}

	private void RegisterEntity(NetworkEntity entity) {
		if (EntitiesById.TryGetValue(entity.Id, out NetworkEntity? existingEntity) &&
		    !ReferenceEquals(existingEntity, entity)) {
			Entities.Remove(existingEntity);
		}

		Entities.Add(entity);
		EntitiesById[entity.Id] = entity;
		entity.Peer = this;
	}

	private void UnregisterEntity(NetworkEntity entity) {
		Entities.Remove(entity);
		EntitiesById.Remove(entity.Id);
		OwnedEntityIds.Remove(entity.Id);
		entity.Peer = null;
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
