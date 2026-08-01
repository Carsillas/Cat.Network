namespace Cat.Network;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class NetworkObjectAttribute : Attribute {
	public ushort Version { get; set; }
}
