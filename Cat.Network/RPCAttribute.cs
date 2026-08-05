namespace Cat.Network;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class RPCAttribute(NetworkMessageReceiveMode receiveMode = NetworkMessageReceiveMode.Event) : Attribute {
	public NetworkMessageReceiveMode ReceiveMode { get; } = receiveMode;
}
