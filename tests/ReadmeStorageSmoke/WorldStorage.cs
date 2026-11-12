using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Cat.Network;

public sealed class WorldStorage : EntityStorage {
	private readonly Dictionary<Guid, NetworkEntity> entities = [];

	protected override IEnumerable<NetworkEntity> GetRegisteredEntities() => entities.Values;
	protected override void AddEntity(NetworkEntity entity) => entities.Add(entity.Id, entity);
	protected override void RemoveEntity(Guid id) => entities.Remove(id);
	public override bool TryGetEntity(Guid id, [NotNullWhen(true)] out NetworkEntity? entity) => entities.TryGetValue(id, out entity);

	public override void PopulateRelevantEntities(NetworkProfile profile, ICollection<NetworkEntity> results) {
		foreach (NetworkEntity entity in entities.Values) {
			results.Add(entity);
		}
	}
}
