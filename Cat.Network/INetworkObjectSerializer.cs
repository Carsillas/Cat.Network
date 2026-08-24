namespace Cat.Network;

public interface INetworkObjectSerializer {
	void Serialize(BufferWriter writer, NetworkObject target, SerializationContext context, SerializationOptions options);
	void Deserialize(NetworkObject target, ReadOnlySpan<byte> data, SerializationContext context);
	void ClearDirtyState(NetworkObject target, SerializationContext context);
}
