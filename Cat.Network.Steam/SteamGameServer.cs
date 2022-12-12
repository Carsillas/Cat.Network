using Steamworks;
using Steamworks.Data;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Cat.Network.Steam {
	public class SteamGameServer : Server, ISocketManager, IDisposable {

		private SocketManager SocketManager { get; set; }
		private Dictionary<uint, Client> ConnectedClients { get; } = new Dictionary<uint, Client>();

		public int BytesReceived { get; private set; }

		public SteamGameServer(IEntityStorage entityStorage) : base(entityStorage) {
			SocketManager = SteamNetworkingSockets.CreateRelaySocket<SocketManager>();
			SocketManager.Interface = this;
		}

		protected override void PreTick() {
			SocketManager?.Receive();
		}

		protected virtual SteamProfileEntity CreateProfileEntity(Friend friend) {
			return new SteamProfileEntity();
		}

		public void Dispose() {
			SocketManager?.Close();
			SocketManager = null;
		}

		void ISocketManager.OnConnecting(Connection connection, ConnectionInfo info) {
			connection.Accept();
		}

		void ISocketManager.OnConnected(Connection connection, ConnectionInfo info) {
			if (!ConnectedClients.ContainsKey(connection.Id)) {
				SteamTransport transport = new SteamTransport {
					Connection = connection
				};

				ConnectedClients.Add(connection.Id, new Client {
					Connection = connection,
					SteamId = info.Identity.SteamId,
					Transport = transport
				});

				Friend friend = new Friend(info.Identity.SteamId);
				SteamProfileEntity profileEntity = CreateProfileEntity(friend);
				profileEntity.Name.Value = friend.Name;
				profileEntity.Id.Value = info.Identity.SteamId.Value;

				AddTransport(transport, profileEntity);
			}
		}

		void ISocketManager.OnDisconnected(Connection connection, ConnectionInfo info) {
			if(ConnectedClients.TryGetValue(connection.Id, out Client client)) {
				RemoveTransport(client.Transport);
			}
			ConnectedClients.Remove(connection.Id);
		}

		void ISocketManager.OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel) {
			byte[] packet = new byte[size];
			BytesReceived += size;
			Marshal.Copy(data, packet, 0, size);

			if (ConnectedClients.TryGetValue(connection.Id, out Client client)) {
				client.Transport.DeliverPacket(packet);
			}
		}

		private struct Client {
			public SteamId SteamId { get; set; }
			public Connection Connection { get; set; }
			public SteamTransport Transport { get; set; }
		}
	}
}
