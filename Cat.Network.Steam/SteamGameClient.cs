using System;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network.Steam {
	public abstract class SteamGameClient : Client, IDisposable {

		public SteamGameClient(IProxyManager proxyManager) : base(proxyManager) {

		}
		
		public abstract void Dispose();
	}
}
