using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network.Steam {
	public class HostTransport : ITransport {
		private Queue<RequestBuffer> Messages { get; } = new Queue<RequestBuffer>();
		public HostTransport Remote { get; set; }

		public void SendPacket(RequestBuffer bytes) {
			Remote.Messages.Enqueue(bytes);
		}

		public bool TryReadPacket(out byte[] bytes) {
			bytes = null;
			if (Messages.TryDequeue(out RequestBuffer buffer)) {
				bytes = new byte[buffer.ByteCount];
				Buffer.BlockCopy(buffer.Buffer, 0, bytes, 0, buffer.ByteCount);

				return true;
			}
			return false;
		}
	}
}
