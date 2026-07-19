using System.Buffers.Binary;
using System.Buffers.Text;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace Cat.Network;

public sealed class Utf8JsonEntitySerializer : IEntitySerializer {
	
	private byte[] Buffer { get; } = new byte[1024];

	private static ReadOnlySpan<byte> EncodedQuote => "\""u8;
	private static ReadOnlySpan<byte> EncodedColon => ":"u8;
	
	
	public void Serialize(Stream stream, NetworkObject entity) {
		
		
		
	}
	

	private void Write(Stream stream, NetworkPropertyInfo info) {
		stream.Write(EncodedQuote);
		stream.Write(info.EncodedName.AsSpan());
		stream.Write(EncodedQuote);
		stream.Write(EncodedColon);
	}
	
	private void WriteValue(Stream stream, Guid value) { }
	private void WriteValue(Stream stream, bool value) { }
	
	private void WriteValue(Stream stream, byte value) { }
	private void WriteValue(Stream stream, sbyte value) { }
	private void WriteValue(Stream stream, ushort value) { }
	private void WriteValue(Stream stream, short value) { }
	private void WriteValue(Stream stream, uint value) { }
	private void WriteValue(Stream stream, int value) { }
	private void WriteValue(Stream stream, ulong value) { }
	private void WriteValue(Stream stream, long value) { }
	private void WriteValue(Stream stream, float value) { }
	private void WriteValue(Stream stream, double value) { }
	private void WriteValue(Stream stream, decimal value) { }
	
	private void WriteValue(Stream stream, DateTime value) { }
	private void WriteValue(Stream stream, DateTimeOffset value) { }
	private void WriteValue(Stream stream, TimeSpan value) { }

	private void WriteValue(Stream stream, string value) { }
}
