using System.Diagnostics.CodeAnalysis;

namespace Cat.Network;

public interface IEntityStorage {

	public bool RegisterEntity(NetworkEntity entity);
	public bool UnregisterEntity(Guid id);
	public bool TryGetEntity(Guid id, [NotNullWhen(true)]  out NetworkEntity? entity);
	public void PopulateRelevantEntities(NetworkProfile profile, ICollection<NetworkEntity> entities);

}
