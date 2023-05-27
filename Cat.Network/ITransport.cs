using System;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network;

public delegate void PacketProcessor(ReadOnlySpan<byte> packet);

public interface ITransport {

	void ReadIncomingPackets(PacketProcessor packetProcessor);

	void SendPacket(byte[] buffer, int count);
		
}
