using Cat.Network.Entities;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Serialization;
internal struct PacketWriter { // maybe type safe for create/update/delete?

	private byte[] Buffer { get; }

	public PacketWriter(byte[] buffer) {
		Buffer = buffer;
	}

	public PacketTargetWriter WriteRequestType(RequestType requestType) {
		Buffer[0] = (byte)requestType;
		return default;
	}
}

internal struct PacketTargetWriter {

	private byte[] Buffer { get; }

	public PacketTargetWriter(byte[] buffer) {
		Buffer = buffer;
	}


	public PacketContentsWriter WriteTarget(NetworkEntity networkEntity) {
		networkEntity.NetworkID.TryWriteBytes(new Span<byte>(Buffer, 1, 16));
		return default;
	}
}

internal struct PacketContentsWriter {

	private const int LengthPosition = 17;
	private const int InitialPosition = 21;

	internal int Position { get; } = InitialPosition;
	private byte[] Buffer { get; }

	public PacketContentsWriter(byte[] buffer) {
		Buffer = buffer;
	}

	public Packet Lock() {
		BinaryPrimitives.WriteInt32LittleEndian(new Span<byte>(Buffer, LengthPosition, 4), Position - InitialPosition);
		return new Packet {
			Length = Position
		};
	}

}

internal struct Packet {
	public int Length { get; init; }

}