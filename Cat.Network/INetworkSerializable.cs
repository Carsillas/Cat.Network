using System;

namespace Cat.Network;

public interface INetworkSerializable {
	
	INetworkEntity Anchor { get; }
	ISerializationContext SerializationContext => Anchor.SerializationContext;
	
	void Initialize();
	int Serialize(SerializationOptions serializationOptions, Span<byte> buffer);
	void Deserialize(SerializationOptions serializationOptions, ReadOnlySpan<byte> buffer);
	
	// Should clean set the LastSetTick and LastUpdateTick of all network properties as well as call clean for NetworkDataObjects?
	void Clean();
	
	// NetworkDataObjects will call into parent for LastModifyTick
	NetworkPropertyInfo[] NetworkProperties { get; set; }
}