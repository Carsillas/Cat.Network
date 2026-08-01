namespace Cat.Network;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class UpgradeToAttribute(ushort version) : Attribute {
	public ushort Version { get; } = version;
}
