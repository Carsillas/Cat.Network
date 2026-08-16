namespace Cat.Network;

public static class NetworkEntityExtensions {
	public static void AssignNetworkId(this NetworkEntity entity, Guid id) {
		ArgumentNullException.ThrowIfNull(entity);

		if (entity.Peer is not null) {
			throw new InvalidOperationException("Cannot assign a network id to an entity attached to a relay peer.");
		}

		entity.Id = id;
	}
}
