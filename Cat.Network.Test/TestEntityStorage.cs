using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cat.Network.Entities;
using Cat.Network.Server;

namespace Cat.Network.Test
{
    public class TestEntityStorage : IEntityStorage {
		private Dictionary<Guid, NetworkEntity> Entities { get; } = new Dictionary<Guid, NetworkEntity>();

		public void RegisterEntity(NetworkEntity entity) {
			Entities.Add(entity.NetworkID, entity);
		}

		public void UnregisterEntity(Guid entityNetworkID) {
			Entities.Remove(entityNetworkID);
		}

		public bool TryGetEntityByNetworkID(Guid entityNetworkID, out NetworkEntity entity) {
			return Entities.TryGetValue(entityNetworkID, out entity);
		}

		public HashSet<NetworkEntity> GetRelevantEntities(NetworkEntity entity) {
			return Entities.Values.ToHashSet();
		}

		public void ProcessRelevantEntities(NetworkEntity entity, IEntityProcessor processor) {
			throw new NotImplementedException();
		}
	}
}
