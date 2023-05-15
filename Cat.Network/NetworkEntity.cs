using System;

namespace Cat.Network;

public interface INetworkEntity {

	Guid NetworkID { get; }

	void Initialize();

	int Serialize(SerializationOptions serializationOptions, Span<byte> buffer);
	void Deserialize(SerializationOptions serializationOptions, ReadOnlySpan<byte> buffer);

	void Clean();

	NetworkPropertyInfo[] NetworkProperties { get; set; }
	ISerializationContext SerializationContext { get; set; }

	int LastDirtyTick { get; set; }

}

public abstract partial class NetworkEntity : IEquatable<NetworkEntity> {
		
	public Guid NetworkID { get; internal set; }
	public bool IsOwner { get; internal set; } = true;

	bool NetworkProperty.DestroyWithOwner { get; set; }

	ISerializationContext INetworkEntity.SerializationContext { get; set; }
	NetworkPropertyInfo[] INetworkEntity.NetworkProperties { get; set; }
	int INetworkEntity.LastDirtyTick { get; set; }

	public NetworkEntity() {
		((INetworkEntity)this).Initialize();
	}

	public bool Equals(NetworkEntity other) {
		return NetworkID == other.NetworkID;
	}

}


