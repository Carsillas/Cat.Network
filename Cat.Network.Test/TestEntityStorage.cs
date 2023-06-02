using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cat.Network.Test {
	public class TestEntityStorage : IEntityStorage {
		private Dictionary<Guid, NetworkEntity> Entities { get; } = new Dictionary<Guid, NetworkEntity>();

		private HashSet<NetworkEntity> SetOperationBuffer { get; } = new HashSet<NetworkEntity>();

		public void RegisterEntity(NetworkEntity entity) {
			Entities.Add(entity.NetworkID, entity);
		}

		public void UnregisterEntity(Guid entityNetworkID) {
			Entities.Remove(entityNetworkID, out NetworkEntity entity);
		}

		public bool TryGetEntityByNetworkID(Guid entityNetworkID, out NetworkEntity entity) {
			return Entities.TryGetValue(entityNetworkID, out entity);
		}

		public void ProcessRelevantEntities(NetworkEntity profileEntity, IEntityProcessor processor) {

			SetOperationBuffer.Clear();

			foreach (NetworkEntity entity in Entities.Values) {
				processor.CreateOrUpdate(entity);
				SetOperationBuffer.Add(entity);
			}

			foreach (NetworkEntity entity in processor.RelevantEntities) {
				if (SetOperationBuffer.Add(entity)) {
					processor.DeleteEntity(entity);
				}
			}
		}



	}
}
