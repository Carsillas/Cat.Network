using Steamworks;
using Steamworks.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Cat.Network.Steam {
	public class SteamGameServer : CatServer, ISocketManager, IDisposable {

		private SocketManager SocketManager { get; set; }
		private Dictionary<uint, Client> ConnectedClients { get; } = new();

		public SteamGameServer(ILogger logger, IEntityStorage entityStorage) : base(logger, entityStorage) {
			SocketManager = SteamNetworkingSockets.CreateRelaySocket<SocketManager>();
			SocketManager.Interface = this;
		}

		protected override void PreExecute() {
			base.PreExecute();

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
				profileEntity.Name = friend.Name;
				profileEntity.Id = info.Identity.SteamId.Value;

				AddTransport(transport, profileEntity);
			}
		}

		void ISocketManager.OnDisconnected(Connection connection, ConnectionInfo info) {
			if (ConnectedClients.TryGetValue(connection.Id, out Client client)) {
				RemoveTransport(client.Transport);
			}
			ConnectedClients.Remove(connection.Id);
		}

		void ISocketManager.OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel) {
			if (ConnectedClients.TryGetValue(connection.Id, out Client client)) {
				unsafe {
					//DeliverPacket(client.Transport, new ReadOnlySpan<byte>((byte*)data, size));
				}
			}
		}

		private struct Client {
			public SteamId SteamId { get; set; }
			public Connection Connection { get; set; }
			public SteamTransport Transport { get; set; }
		}
	}
}
