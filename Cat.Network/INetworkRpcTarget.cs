namespace Cat.Network;

public interface INetworkRpcTarget {
	bool TryInvokeRpc(RelayClient client, NetworkProfile instigator, ulong rpcId, ReadOnlySpan<byte> data, SerializationContext context);
	bool TryInvokeBroadcast(RelayClient client, NetworkProfile instigator, ulong rpcId, ReadOnlySpan<byte> data, SerializationContext context);
}
