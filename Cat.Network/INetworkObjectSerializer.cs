namespace Cat.Network;

public interface INetworkObjectSerializer {
	void Serialize(NetworkObject target);
	void Deserialize(NetworkObject target, ReadOnlySpan<byte> data, SerializationContext context);
}
