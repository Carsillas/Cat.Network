using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network.Steam {
	public class HostTransport : ITransport {
		private ConcurrentQueue<byte[]> Messages { get; } = new ConcurrentQueue<byte[]>();
		public HostTransport Remote { get; set; }

		private bool TryReadPacket(out byte[] bytes) {
			return Messages.TryDequeue(out bytes);
		}

		public void SendPacket(byte[] buffer, int count) {
			byte[] copy = new byte[count];
			Buffer.BlockCopy(buffer, 0, copy, 0, count);
			Remote.Messages.Enqueue(copy);
		}

		public void ReadIncomingPackets(PacketProcessor packetProcessor) {
			while(TryReadPacket(out byte[] bytes)) {
				packetProcessor?.Invoke(bytes);
			}

		}
	}
}
