using Steamworks;
using Steamworks.Data;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using FacepunchClient = Steamworks.SteamClient;

namespace Cat.Network.Steam {
	public abstract class SteamGameClient : CatClient, IConnectionManager, IDisposable {
		private ConnectionManager ConnectionManager { get; set; }
		private SteamTransport Transport { get; }

		public ConnectionStatus? Status => Transport?.Connection.QuickStatus() ?? null;
		public string DetailedStatus => Transport?.Connection.DetailedStatus() ?? null;

		public SteamGameClient(SteamGameServer server, ILogger logger, IProxyManager proxyManager) : base(logger, proxyManager) {
			HostTransport clientTransport = new HostTransport();
			HostTransport serverTransport = new HostTransport();

			clientTransport.Remote = serverTransport;
			serverTransport.Remote = clientTransport;

			SteamProfileEntity profileEntity = new SteamProfileEntity {
				Name = FacepunchClient.Name,
				Id = FacepunchClient.SteamId.Value
			};

			server.AddTransport(clientTransport, profileEntity);
			Connect(serverTransport);
		}

		public SteamGameClient(ulong targetSteamId, ILogger logger, IProxyManager proxyManager) : base(logger, proxyManager) {
			if (FacepunchClient.SteamId != targetSteamId) {
				Transport = new SteamTransport();
				ConnectionManager = SteamNetworkingSockets.ConnectRelay<ConnectionManager>(targetSteamId);
				ConnectionManager.Interface = this;
			} else {
				// ???
			}
		}


		protected override void PreExecute() {
			base.PreExecute();

			ConnectionManager?.Receive();
		}

		public void Dispose() {
			ConnectionManager?.Close();
			ConnectionManager = null;
		}

		void IConnectionManager.OnConnecting(ConnectionInfo info) {

		}

		void IConnectionManager.OnConnected(ConnectionInfo info) {
			Transport.Connection = ConnectionManager.Connection;
			Connect(Transport);
		}

		void IConnectionManager.OnDisconnected(ConnectionInfo info) {

		}

		void IConnectionManager.OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel) {
			unsafe {
				//DeliverPacket(new ReadOnlySpan<byte>((byte*)data, size));
			}
		}

	}
}
