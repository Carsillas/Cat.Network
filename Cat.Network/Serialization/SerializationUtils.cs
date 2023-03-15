using Cat.Network.Server;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Serialization;
internal static class SerializationUtils {
	public static void ExtractPacketHeader(ReadOnlySpan<byte> bytes, out Guid networkID, out ReadOnlySpan<byte> content) {
		networkID = new Guid(bytes.Slice(0, 16));
		int length = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(16, 4));
		content = bytes.Slice(20, length);
	}


	public delegate void ClientRequestProcessor(Guid networkID, ReadOnlySpan<byte> content);
	public delegate void ServerRequestProcessor(RemoteClient remoteClient, Guid networkID, ReadOnlySpan<byte> content);

}
