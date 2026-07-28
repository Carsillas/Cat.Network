namespace Cat.Network;

public interface INetworkCollection {
	void Serialize(BufferWriter writer, SerializationContext context, SerializationOptions options);

	void Deserialize(ReadOnlySpan<byte> data, SerializationContext context);
}
