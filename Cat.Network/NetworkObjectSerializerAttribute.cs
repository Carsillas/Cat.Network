namespace Cat.Network;

[AttributeUsage(AttributeTargets.Class)]
public class NetworkObjectSerializerAttribute<T> : Attribute where T : INetworkObjectSerializer {
	
	private Type Type { get; } = typeof(T);
	
}