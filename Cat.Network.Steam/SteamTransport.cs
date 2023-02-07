using Steamworks.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network.Steam {
	internal class SteamTransport : ITransport {
		private ConcurrentQueue<byte[]> Packets { get; } = new ConcurrentQueue<byte[]>();
		public Connection Connection { get; set; }

		public void DeliverPacket(byte[] bytes) {
			Packets.Enqueue(bytes);
		}

		public void SendPacket(RequestBuffer request) {
			Connection.SendMessage(request.Buffer, 0, request.ByteCount, SendType.Reliable);
		}

		public bool TryReadPacket(out byte[] bytes) {
			return Packets.TryDequeue(out bytes);
		}
	}
}
