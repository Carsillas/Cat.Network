using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network.Steam {
	public class HostTransport : ITransport {
		private ConcurrentQueue<byte[]> Messages { get; } = new ConcurrentQueue<byte[]>();
		public HostTransport Remote { get; set; }

		public void SendPacket(byte[] bytes) {
			Remote.Messages.Enqueue(bytes);
		}

		public bool TryReadPacket(out byte[] bytes) {
			return Messages.TryDequeue(out bytes);
		}
	}
}
