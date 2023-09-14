using System;

namespace Cat.Network;


public interface INetworkSerializable {
	
	ISerializationContext SerializationContext { get; }
	
	void Initialize();
	int Serialize(SerializationOptions serializationOptions, Span<byte> buffer);
	void Deserialize(SerializationOptions serializationOptions, ReadOnlySpan<byte> buffer);
	
	// Should clean set the LastSetTick and LastUpdateTick of all network properties as well as call clean for NetworkDataObjects?
	void Clean();
	
	// NetworkDataObjects will call into parent for LastModifyTick
	NetworkPropertyInfo[] NetworkProperties { get; set; }
}

public interface INetworkEntity : INetworkSerializable {
	
	ISerializationContext INetworkSerializable.SerializationContext => SerializationContext;
	new ISerializationContext SerializationContext { get; set; }
	
	Guid NetworkID { get; }

	void HandleRPCInvocation(NetworkEntity instigator, ReadOnlySpan<byte> buffer);
	

	int LastDirtyTick { get; set; }

}

public abstract partial class NetworkEntity : IEquatable<NetworkEntity> {
		
	public Guid NetworkID { get; internal set; }
	public bool IsOwner { get; internal set; } = true;
	public bool IsSpawned { get; internal set; }

	bool NetworkProperty.DestroyWithOwner { get; set; }
	
	ISerializationContext INetworkEntity.SerializationContext { get; set; }
	
	NetworkPropertyInfo[] INetworkSerializable.NetworkProperties { get; set; }
	int INetworkEntity.LastDirtyTick { get; set; }

	public NetworkEntity() {
		((INetworkEntity)this).Initialize();
	}

	public bool Equals(NetworkEntity other) {
		return NetworkID == other.NetworkID;
	}

}


