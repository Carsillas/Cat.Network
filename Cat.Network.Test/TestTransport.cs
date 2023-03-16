using System;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network.Test {
	public class TestTransport : ITransport {
		private Queue<byte[]> Messages { get; } = new Queue<byte[]>();
		public TestTransport Remote { get; set; }

		public void SendPacket(byte[] bytes) {
			Remote.Messages.Enqueue(bytes);
		}

		public bool TryReadPacket(out byte[] bytes) {
			return Messages.TryDequeue(out bytes);
		}

		public void SendPacket(byte[] buffer, int count) {
			throw new NotImplementedException();
		}

		public void ProcessPackets(ITransport.PacketProcessor processor) {
			throw new NotImplementedException();
		}
	}
}
