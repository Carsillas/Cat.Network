using System;
using System.Collections.Generic;
using System.Text;

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
