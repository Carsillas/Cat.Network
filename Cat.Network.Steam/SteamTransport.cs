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

		public void SendPacket(byte[] buffer, int count) {

			Connection.SendMessage(buffer, 0, count, SendType.Reliable);
		}

		public bool TryReadPacket(out byte[] bytes) {
			return Packets.TryDequeue(out bytes);
		}

		public void ProcessPackets(ITransport.PacketProcessor processor) {
		

		}
	}
}
