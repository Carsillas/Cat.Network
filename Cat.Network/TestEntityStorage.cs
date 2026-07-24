using System.Diagnostics.CodeAnalysis;

namespace Cat.Network;

public class TestEntityStorage : IEntityStorage {
	
	private Dictionary<Guid, NetworkEntity> Entities { get; } = new();
	
	public bool RegisterEntity(NetworkEntity entity) {
		return Entities.TryAdd(entity.Id, entity);
	}
	public bool TryGetEntity(Guid id, [NotNullWhen(true)] out NetworkEntity? entity) {
		return Entities.TryGetValue(id, out entity);
	}
}