using System;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network {

	public interface ITransport {

		delegate void PacketProcessor(ReadOnlySpan<byte> packet);

		void SendPacket(byte[] buffer, int count);
		void ProcessPackets(PacketProcessor processor);
			
	}
}
