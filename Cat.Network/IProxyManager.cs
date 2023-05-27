using System;

namespace Cat.Network;

public interface IProxyManager {
	void OnEntityCreated(NetworkEntity entity);
	void OnEntityDeleted(NetworkEntity entity);

	void OnGainedOwnership(NetworkEntity entity);
}
