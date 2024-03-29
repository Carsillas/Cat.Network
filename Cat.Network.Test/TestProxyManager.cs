﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network.Test
{
    public class TestProxyManager : IProxyManager {

		public event Action<NetworkEntity> GainedOwnership;
		public event Action<NetworkEntity> ForfeitedOwnership;

		public void Dispose() {

		}

		public void OnEntityCreated(NetworkEntity entity) {

		}

		public void OnEntityDeleted(NetworkEntity entity) {

		}

		public void OnGainedOwnership(NetworkEntity entity) {
			GainedOwnership?.Invoke(entity);
		}

		public void OnForfeitOwnership(NetworkEntity entity) {
			ForfeitedOwnership?.Invoke(entity);
		}
    }
}
