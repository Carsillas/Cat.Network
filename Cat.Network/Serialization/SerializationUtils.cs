﻿using Cat.Network.Server;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Serialization;
internal static class SerializationUtils {
	public static void ExtractPacketHeader(ReadOnlySpan<byte> bytes, out RequestType requestType, out Guid networkID, out ReadOnlySpan<byte> content) {
		requestType = (RequestType) bytes[0];
		networkID = new Guid(bytes.Slice(1, 16));

		if(bytes.Length < 21 ) {
			content = ReadOnlySpan<byte>.Empty;
			return;
		}

		int length = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(17, 4));
		content = bytes.Slice(21, length);
	}

	public static void WritePacketHeader(Span<byte> bytes, RequestType type, Guid networkID) {
		bytes[0] = (byte) type;
		networkID.TryWriteBytes(bytes.Slice(1, 16));
	}

	public static Span<byte> GetContentSpan(byte[] buffer) {
		return new Span<byte>(buffer, 17, buffer.Length - 17);
	}


	public static int HeaderLength = 17;



	public delegate void ClientRequestProcessor(Guid networkID, ReadOnlySpan<byte> content);
	public delegate void ServerRequestProcessor(RemoteClient remoteClient, Guid networkID, ReadOnlySpan<byte> content);

}