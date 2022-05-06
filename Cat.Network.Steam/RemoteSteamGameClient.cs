using Steamworks;
using Steamworks.Data;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using FacepunchClient = Steamworks.SteamClient;

namespace Cat.Network.Steam {
	public class RemoteSteamGameClient : SteamGameClient, IConnectionManager {

		private ConnectionManager ConnectionManager { get; set; }
		private SteamTransport Transport { get; } = new SteamTransport();

		public RemoteSteamGameClient(ulong targetSteamId, IProxyManager proxyManager) : base(proxyManager) {
			if (FacepunchClient.SteamId != targetSteamId) {
				ConnectionManager = SteamNetworkingSockets.ConnectRelay<ConnectionManager>(targetSteamId);
				ConnectionManager.Interface = this;
			} else {
				// ???
			}
		}

		protected override void PreTick() {
			base.PreTick();

			ConnectionManager?.Receive();
		}

		public override void Dispose() {
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
			byte[] packet = new byte[size];
			Marshal.Copy(data, packet, 0, size);
			Transport.DeliverPacket(packet);
		}

	}
}
