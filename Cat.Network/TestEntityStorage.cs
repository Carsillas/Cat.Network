using System.Diagnostics.CodeAnalysis;

namespace Cat.Network;

public class TestEntityStorage : EntityStorage {
	
	private Dictionary<Guid, NetworkEntity> Entities { get; } = new();
	
	protected override IEnumerable<NetworkEntity> GetRegisteredEntities() {
		return Entities.Values;
	}

	protected override void AddEntity(NetworkEntity entity) {
		Entities.Add(entity.Id, entity);
	}

	protected override void RemoveEntity(Guid id) {
		Entities.Remove(id);
	}

	public override bool TryGetEntity(Guid id, [NotNullWhen(true)] out NetworkEntity? entity) {
		return Entities.TryGetValue(id, out entity);
	}

	public override void PopulateRelevantEntities(NetworkProfile profile, ICollection<NetworkEntity> entities) {
		foreach (NetworkEntity entity in Entities.Values) {
			entities.Add(entity);
		}
	}
}
