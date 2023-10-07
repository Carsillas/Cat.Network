using Steamworks.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network.Steam {
	internal class SteamTransport : ITransport {
		public Connection Connection { get; set; }

		public void ReadIncomingPackets(PacketProcessor packetProcessor) {
			throw new NotImplementedException();
		}

		public void SendPacket(byte[] buffer, int count) {
			Connection.SendMessage(buffer, 0, count, SendType.Reliable);
		}

	}
}
