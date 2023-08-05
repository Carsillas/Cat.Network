using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network;
public class LocalTransportPair {

	public ITransport Server { get; }
	public ITransport Client { get; }

	public LocalTransportPair() {

		LocalTransport server = new LocalTransport();
		LocalTransport client = new LocalTransport();

		server.Remote = client;
		client.Remote = server;

		Server = server;
		Client = client;
	}	

	private class LocalTransport : ITransport {

		BufferPool BufferPool { get; } = new();
		internal Queue<byte[]> Messages { get; } = new();
		public LocalTransport Remote { get; set; }

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
