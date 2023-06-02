using System;

namespace Cat.Network;

public interface IEntityStorage {
	void RegisterEntity(NetworkEntity entity);
	void UnregisterEntity(Guid entityNetworkID);

	bool TryGetEntityByNetworkID(Guid entityNetworkID, out NetworkEntity entity);

	void ProcessRelevantEntities(NetworkEntity profileEntity, IEntityProcessor processor);
}
