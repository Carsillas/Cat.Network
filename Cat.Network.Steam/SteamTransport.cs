using Steamworks.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network.Steam {
	public class SteamTransport : ITransport {
		private ConcurrentQueue<byte[]> Packets { get; } = new ConcurrentQueue<byte[]>();
		public Connection Connection { get; }

		public SteamTransport(Connection connection) {
			Connection = connection;
		}

		public void DeliverPacket(byte[] bytes) {
			Packets.Enqueue(bytes);
		}

		public void SendPacket(byte[] bytes) {
			Connection.SendMessage(bytes, 0, bytes.Length, SendType.Reliable);
		}

		public bool TryReadPacket(out byte[] bytes) {
			return Packets.TryDequeue(out bytes);
		}
	}
}
