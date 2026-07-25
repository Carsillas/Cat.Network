namespace Cat.Network;

public interface INetworkObject {
	int PropertyIndex { get; set; }
	INetworkAnchor? Anchor { get; }
	NetworkObject? Parent { get; set; }
}