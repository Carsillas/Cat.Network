using System;

namespace Cat.Network;

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


