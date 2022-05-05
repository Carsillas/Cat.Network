using System;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network {
	public interface IEntityStorage {
		void RegisterEntity(NetworkEntity entity);
		void UnregisterEntity(Guid entityNetworkID);

		bool TryGetEntityByNetworkID(Guid entityNetworkID, out NetworkEntity entity);

		HashSet<NetworkEntity> GetRelevantEntities(NetworkEntity entity);

	}
}
