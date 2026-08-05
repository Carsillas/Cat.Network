using System.Diagnostics.CodeAnalysis;

namespace Cat.Network;

public class TestEntityStorage : IEntityStorage {
	
	private Dictionary<Guid, NetworkEntity> Entities { get; } = new();
	
	public bool RegisterEntity(NetworkEntity entity) {
		return Entities.TryAdd(entity.Id, entity);
	}

	public bool UnregisterEntity(Guid id) {
		return Entities.Remove(id);
	}

	public bool TryGetEntity(Guid id, [NotNullWhen(true)] out NetworkEntity? entity) {
		return Entities.TryGetValue(id, out entity);
	}

	public virtual void PopulateRelevantEntities(NetworkProfile profile, ICollection<NetworkEntity> entities) {
		foreach (NetworkEntity entity in Entities.Values) {
			entities.Add(entity);
		}
	}
}
