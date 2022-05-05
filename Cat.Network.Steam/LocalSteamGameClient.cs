using System;
using System.Collections.Generic;
using System.Text;
using FacepunchClient = Steamworks.SteamClient;

namespace Cat.Network.Steam {
	public class LocalSteamGameClient : Client, IDisposable {

		public LocalSteamGameClient(SteamGameServer server) {
			HostTransport clientTransport = new HostTransport();
			HostTransport serverTransport = new HostTransport();

			clientTransport.Remote = serverTransport;
			serverTransport.Remote = clientTransport;

			SteamProfileEntity profileEntity = new SteamProfileEntity();
			profileEntity.Name.Value = FacepunchClient.Name;
			profileEntity.Id.Value = FacepunchClient.SteamId.Value;

			server.AddTransport(clientTransport, profileEntity);
			Connect(serverTransport);
		}

		void IDisposable.Dispose() {

		}
	}
}
