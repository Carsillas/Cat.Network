using System;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network {

	public interface ITransport {

		delegate void PacketProcessor(ReadOnlySpan<byte> packet);

		void SendPacket(Span<byte> bytes);
		void ProcessPackets(PacketProcessor processor);
			
	}
}
