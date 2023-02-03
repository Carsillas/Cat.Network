using System;
using System.Collections.Generic;
using System.Text;
using Cat.Network.Entities;

namespace Cat.Network
{
    public interface IProxyManager : IDisposable {
		void OnEntityCreated(NetworkEntity entity);
		void OnEntityDeleted(NetworkEntity entity);

		void OnGainedOwnership(NetworkEntity entity);
	}
}
