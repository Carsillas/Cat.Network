namespace Cat.Network;

public class NetworkObjectSerializerAttribute : Attribute {
	private protected NetworkObjectSerializerAttribute(Type serializerType) {
		SerializerType = serializerType;
	}
	public Type SerializerType { get; }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class NetworkObjectSerializerAttribute<T>() : NetworkObjectSerializerAttribute(typeof(T)) where T : INetworkObjectSerializer;
