using System;

namespace Cat.Network;

public abstract partial class NetworkEntity : IEquatable<NetworkEntity> {
		
	public Guid NetworkId { get; internal set; }
	public bool IsOwner { get; internal set; } = true;
	public bool IsSpawned { get; internal set; }

	public event Action<object, PropertyChangedEventArgs> PropertyChanged;
	
	bool NetworkProperty.DestroyWithOwner { get; set; }
	
	ISerializationContext INetworkEntity.SerializationContext { get; set; }
	
	NetworkPropertyInfo[] INetworkSerializable.NetworkProperties { get; set; }
	int INetworkEntity.LastDirtyTick { get; set; }

	protected NetworkEntity() {
		((INetworkEntity)this).Initialize();
	}

	public bool Equals(NetworkEntity other) {
		return NetworkId == other.NetworkId;
	}
	
	void INetworkSerializable.OnPropertyChanged(PropertyChangedEventArgs args) {
		PropertyChanged?.Invoke(this, args);
	}
	
}


