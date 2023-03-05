using System;
using System.Collections.Generic;
using System.Text;
using Cat.Network.Entities;

namespace Cat.Network
{
    public interface IEntityStorage {
		void RegisterEntity(NetworkEntity entity);
		void UnregisterEntity(Guid entityNetworkID);

		bool TryGetEntityByNetworkID(Guid entityNetworkID, out NetworkEntity entity);

		void ProcessRelevantEntities(NetworkEntity entity, Action<NetworkEntity> processFunction);

	}
}
