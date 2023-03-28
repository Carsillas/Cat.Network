using System;
using System.Collections.Generic;
using System.Text;
using Cat.Network.Entities;

namespace Cat.Network.Server
{
    public interface IEntityStorage
    {
        void RegisterEntity(NetworkEntity entity, NetworkEntity ownerProfileEntity = null);
		void UnregisterEntity(Guid entityNetworkID);

        bool TryGetEntityByNetworkID(Guid entityNetworkID, out NetworkEntity entity);

        void ProcessRelevantEntities(NetworkEntity profileEntity, IEntityProcessor processor);
		bool TryGetOwner(NetworkEntity entity, out NetworkEntity owner);
	}
}
