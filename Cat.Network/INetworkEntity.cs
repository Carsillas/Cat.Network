using System;

namespace Cat.Network;

public interface INetworkEntity : INetworkSerializable {

	INetworkEntity INetworkSerializable.Anchor => this;
	ISerializationContext INetworkSerializable.SerializationContext => SerializationContext;
	new ISerializationContext SerializationContext { get; set; }
	
	Guid NetworkID { get; }
	int LastDirtyTick { get; set; }
	

	void HandleRPCInvocation(NetworkEntity instigator, ReadOnlySpan<byte> buffer);
	
}