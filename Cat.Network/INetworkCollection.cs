namespace Cat.Network;

public interface INetworkCollection {
	void Initialize(NetworkObject owner, int propertyIndex);

	void Serialize(BufferWriter writer, SerializationContext context, SerializationOptions options);

	void Deserialize(ReadOnlySpan<byte> data, SerializationContext context);
}
