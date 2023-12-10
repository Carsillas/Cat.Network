using System;

namespace Cat.Network;

public interface IEntityStorage {

	void Initialize(CatServer server);
	
	void RegisterEntity(NetworkEntity entity);
	void UnregisterEntity(Guid entityNetworkId);

	bool TryGetEntityByNetworkId(Guid entityNetworkId, out NetworkEntity entity);

	void ProcessRelevantEntities(NetworkEntity profileEntity, IEntityProcessor processor);
}
