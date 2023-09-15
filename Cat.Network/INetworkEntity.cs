using System;

namespace Cat.Network;

public interface INetworkEntity : INetworkSerializable {

	INetworkEntity INetworkSerializable.Anchor => this;
	ISerializationContext INetworkSerializable.SerializationContext => SerializationContext;
	
	new ISerializationContext SerializationContext { get; set; }
	
	Guid NetworkID { get; }

	void HandleRPCInvocation(NetworkEntity instigator, ReadOnlySpan<byte> buffer);
	

	int LastDirtyTick { get; set; }

}