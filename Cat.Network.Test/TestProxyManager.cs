using System;
using System.Collections.Generic;
using System.Text;
using Cat.Network.Entities;

namespace Cat.Network.Test
{
    public class TestProxyManager : IProxyManager {

		public event Action<NetworkEntity> GainedOwnership;

		public void Dispose() {

		}

		public void OnEntityCreated(NetworkEntity entity) {

		}

		public void OnEntityDeleted(NetworkEntity entity) {

		}

		public void OnGainedOwnership(NetworkEntity entity) {
			GainedOwnership?.Invoke(entity);
		}
	}
}
