using System;
using System.Collections.Generic;
using Cat.Network.Collections;

namespace Cat.Network;

public interface INetworkDataObject : INetworkSerializable {
	int PropertyIndex { get; set; }
	INetworkSerializable Parent { get; set; }
	INetworkObjectList Collection { get; set; }

}

public partial record NetworkDataObject : INetworkDataObject {
	
	int INetworkDataObject.PropertyIndex { get; set; }
	INetworkSerializable INetworkDataObject.Parent { get; set; }
	INetworkObjectList INetworkDataObject.Collection { get; set; }

	INetworkEntity INetworkSerializable.Anchor => ((INetworkDataObject)this).Parent?.Anchor;
	NetworkPropertyInfo[] INetworkSerializable.NetworkProperties { get; set; }
	
	public event Action<object, PropertyChangedEventArgs> PropertyChanged;


	protected NetworkDataObject() {
		((INetworkSerializable)this).Initialize();
	}
	
	protected NetworkDataObject(NetworkDataObject other) {
		// This implementation is what prevents the declared properties from being copied over
		// to the new instance upon cloning, NetworkProperties for example.
		((INetworkSerializable)this).Initialize();
	}

	void INetworkSerializable.OnPropertyChanged(PropertyChangedEventArgs args) {
		PropertyChanged?.Invoke(this, args);

		INetworkDataObject iNetworkDataObject = this;

		if (iNetworkDataObject.Parent is { } networkSerializableParent && iNetworkDataObject.Collection == null) {
			networkSerializableParent.OnPropertyChanged(new PropertyChangedEventArgs {
				Index = iNetworkDataObject.PropertyIndex,
				Name = networkSerializableParent.NetworkProperties[iNetworkDataObject.PropertyIndex].Name
			});
		}
	}

	public virtual bool Equals(NetworkDataObject other) {
		return other != null;
	}

	public override int GetHashCode() => 0;

}

