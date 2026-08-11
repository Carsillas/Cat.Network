namespace Cat.Network;


[NetworkObject]
public abstract partial class NetworkEntity : NetworkObject, INetworkObject, INetworkRpcTarget {

	public Guid Id { get; internal set; }

	[NetworkProperty]
	public partial bool DestroyWithOwner { get; set; }

	public RelayPeer? Peer { get; internal set; }

	public bool IsSpawned => Peer is not null;

	private protected override NetworkObject? GetAnchor() {
		return this;
	}

	private protected override bool GetIsOwner() {
		return Peer?.Owns(this) ?? false;
	}

	bool INetworkRpcTarget.TryInvokeRpc(RelayClient client, NetworkProfile instigator, ulong rpcId, ReadOnlySpan<byte> data, SerializationContext context) {
		return false;
	}

	bool INetworkRpcTarget.TryInvokeBroadcast(RelayClient client, NetworkProfile instigator, ulong rpcId, ReadOnlySpan<byte> data, SerializationContext context) {
		return false;
	}
	
}
