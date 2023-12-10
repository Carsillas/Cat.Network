using System;

namespace Cat.Network;

public interface INetworkEntity : INetworkSerializable {

	INetworkEntity INetworkSerializable.Anchor => this;
	ISerializationContext INetworkSerializable.SerializationContext => SerializationContext;
	new ISerializationContext SerializationContext { get; set; }
	
	Guid NetworkId { get; }
	int LastDirtyTick { get; set; }
	

	void HandleRpcInvocation(Guid instigatorId, ReadOnlySpan<byte> buffer);
	
}