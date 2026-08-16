using System.Diagnostics.CodeAnalysis;

namespace Cat.Network;

public abstract class EntityStorage {
	private RelayPeer? Peer { get; set; }

	public bool RegisterEntity(NetworkEntity entity) {
		ArgumentNullException.ThrowIfNull(entity);

		if (entity.Id == Guid.Empty) {
			AssignNetworkId(entity, Guid.NewGuid());
		}

		ValidateDetachedEntity(entity);

		if (TryGetEntity(entity.Id, out _)) {
			return false;
		}

		AddEntity(entity);
		entity.Peer = Peer;
		return true;
	}

	public bool UnregisterEntity(Guid id) {
		if (!TryGetEntity(id, out NetworkEntity? entity)) {
			return false;
		}

		RemoveEntity(id);
		entity.Peer = null;
		return true;
	}

	protected void AssignNetworkId(NetworkEntity entity, Guid id) {
		ArgumentNullException.ThrowIfNull(entity);

		if (entity.Peer is not null) {
			throw new InvalidOperationException("Cannot assign a network id to an entity attached to a relay peer.");
		}

		if (TryGetEntity(id, out NetworkEntity? existingEntity) && !ReferenceEquals(existingEntity, entity)) {
			throw new InvalidOperationException($"Entity network id {id} is already registered.");
		}

		entity.Id = id;
	}

	protected abstract IEnumerable<NetworkEntity> GetRegisteredEntities();
	protected abstract void AddEntity(NetworkEntity entity);
	protected abstract void RemoveEntity(Guid id);
	public abstract bool TryGetEntity(Guid id, [NotNullWhen(true)] out NetworkEntity? entity);
	public abstract void PopulateRelevantEntities(NetworkProfile profile, ICollection<NetworkEntity> entities);

	internal void Attach(RelayPeer peer) {
		ArgumentNullException.ThrowIfNull(peer);

		if (Peer is not null && !ReferenceEquals(Peer, peer)) {
			throw new InvalidOperationException("Entity storage is already attached to another relay peer.");
		}

		Peer = peer;

		foreach (NetworkEntity entity in GetRegisteredEntities()) {
			AttachEntity(entity);
		}
	}

	private void AttachEntity(NetworkEntity entity) {
		if (entity.Id == Guid.Empty) {
			throw new InvalidOperationException("Cannot attach a registered network entity with an empty network id.");
		}

		if (entity.Peer is not null && !ReferenceEquals(entity.Peer, Peer)) {
			throw new InvalidOperationException("Cannot attach a network entity that is already attached to another relay peer.");
		}

		entity.Peer = Peer;
	}

	private static void ValidateDetachedEntity(NetworkEntity entity) {
		if (entity.Peer is not null) {
			throw new InvalidOperationException("Cannot register a network entity that is already attached to a relay peer.");
		}
	}
}
