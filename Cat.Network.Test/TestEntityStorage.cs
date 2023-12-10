using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cat.Network.Test {
	public class TestEntityStorage : IEntityStorage {
		private Dictionary<Guid, NetworkEntity> Entities { get; } = new();

		private HashSet<NetworkEntity> SetOperationBuffer { get; } = new();

		public void Initialize(CatServer server) {
			
		}

		public void RegisterEntity(NetworkEntity entity) {
			Entities.Add(entity.NetworkId, entity);
		}

		public void UnregisterEntity(Guid entityNetworkId) {
			Entities.Remove(entityNetworkId, out NetworkEntity entity);
		}

		public bool TryGetEntityByNetworkId(Guid entityNetworkId, out NetworkEntity entity) {
			return Entities.TryGetValue(entityNetworkId, out entity);
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
