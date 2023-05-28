using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cat.Network.Test {
	public class TestEntityStorage : IEntityStorage {
		private Dictionary<Guid, NetworkEntity> Entities { get; } = new Dictionary<Guid, NetworkEntity>();
		private Dictionary<NetworkEntity, NetworkEntity> Owners { get; } = new Dictionary<NetworkEntity, NetworkEntity>();


		private HashSet<NetworkEntity> SetOperationBuffer { get; } = new HashSet<NetworkEntity>();

		public void RegisterEntity(NetworkEntity entity, NetworkEntity ownerProfileEntity) {
			Entities.Add(entity.NetworkID, entity);
			if (ownerProfileEntity != null) {
				Owners.Add(entity, ownerProfileEntity);
			}
		}

		public void UnregisterEntity(Guid entityNetworkID) {
			if (Entities.Remove(entityNetworkID, out NetworkEntity entity)) {
				Owners.Remove(entity);
			}
		}

		public bool TryGetEntityByNetworkID(Guid entityNetworkID, out NetworkEntity entity) {
			return Entities.TryGetValue(entityNetworkID, out entity);
		}

		public void ProcessRelevantEntities(NetworkEntity profileEntity, IEntityProcessor processor) {

			SetOperationBuffer.Clear();

			foreach (NetworkEntity entity in Entities.Values) {
				if (processor.RelevantEntities.Add(entity)) {
					processor.CreateEntity(entity, AssignIfOwnerless(profileEntity, entity));
				} else {
					processor.UpdateEntity(entity, AssignIfOwnerless(profileEntity, entity));
				}
				SetOperationBuffer.Add(entity);
			}


			foreach (NetworkEntity entity in processor.RelevantEntities) {
				if (SetOperationBuffer.Add(entity)) {
					UnassignIfOwned(profileEntity, entity);
					processor.DeleteEntity(entity);
				}
			}
		}

		bool AssignIfOwnerless(NetworkEntity profileEntity, NetworkEntity entity) {
			if (!Owners.TryGetValue(entity, out NetworkEntity currentOwnerProfileEntity) || currentOwnerProfileEntity == null || !Entities.ContainsKey(currentOwnerProfileEntity.NetworkID)) {
				currentOwnerProfileEntity = profileEntity;
				Owners[entity] = profileEntity;
			}

			return profileEntity == currentOwnerProfileEntity;
		}

		void UnassignIfOwned(NetworkEntity profileEntity, NetworkEntity entity) {
			if (Owners.TryGetValue(entity, out NetworkEntity currentOwnerProfileEntity) && currentOwnerProfileEntity == profileEntity) {
				Owners.Remove(entity);
			}
		}


		public bool TryGetOwner(NetworkEntity entity, out NetworkEntity owner) {
			return Owners.TryGetValue(entity, out owner);
		}

		private void SetOwner(NetworkEntity entity, NetworkEntity owner) {

			// no need to remove from previous owner because the only way
			// an entity can switch owners is if its deleted, IEntityProcessor.DeleteEntity
			// will remove it from OwnedEntities.

			if (owner == null) {
				Owners.Remove(entity);
			} else {
				Owners[entity] = owner;
			}
		}

	}
}
