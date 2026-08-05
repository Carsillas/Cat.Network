namespace Cat.Network;


[NetworkObject]
public abstract partial class NetworkEntity : NetworkObject, INetworkObject, INetworkAnchor, INetworkRpcTarget {

	INetworkAnchor INetworkObject.Anchor => this;
	public Guid Id { get; internal set; }

	public RelayPeer? Peer { get; internal set; }

	public bool IsOwner => Peer?.Owns(this) ?? false;

	bool INetworkRpcTarget.TryInvokeRpc(RelayClient client, NetworkProfile instigator, ulong rpcId, ReadOnlySpan<byte> data, SerializationContext context) {
		return false;
	}

	bool INetworkRpcTarget.TryInvokeBroadcast(RelayClient client, NetworkProfile instigator, ulong rpcId, ReadOnlySpan<byte> data, SerializationContext context) {
		return false;
	}
	
}
