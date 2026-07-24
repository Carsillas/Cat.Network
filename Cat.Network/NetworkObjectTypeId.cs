namespace Cat.Network;

public sealed class NetworkObjectTypeId(string id) : Attribute {
	public Guid Id { get; } = Guid.Parse(id);
}
