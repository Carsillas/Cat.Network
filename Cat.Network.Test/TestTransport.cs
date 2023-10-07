using System;
using System.Collections.Generic;

namespace Cat.Network.Test {
	public class TestTransport : ITransport {

		BufferPool BufferPool { get; } = new();
		public Queue<byte[]> Messages { get; } = new();
		public TestTransport Remote { get; set; }

		public void ReadIncomingPackets(PacketProcessor packetProcessor) {
			foreach (byte[] packet in Messages) {
				packetProcessor?.Invoke(packet);
			}
			Messages.Clear();
			BufferPool.FreeAllBuffers();
		}

		public void SendPacket(byte[] buffer, int count) {
			byte[] copy = Remote.BufferPool.RentBuffer();
			Buffer.BlockCopy(buffer, 0, copy, 0, count);
			Remote.Messages.Enqueue(copy);
		}

	}
}
