using System;

namespace Cat.Network;

public interface IEntityStorage {

	void Initialize(CatServer server);
	
	void RegisterEntity(NetworkEntity entity);
	void UnregisterEntity(Guid entityNetworkID);

	bool TryGetEntityByNetworkID(Guid entityNetworkID, out NetworkEntity entity);

	void ProcessRelevantEntities(NetworkEntity profileEntity, IEntityProcessor processor);
}
