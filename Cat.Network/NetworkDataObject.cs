using System;
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

	protected NetworkDataObject() {
		((INetworkSerializable)this).Initialize();
	}
	
	protected NetworkDataObject(NetworkDataObject other) {
		((INetworkSerializable)this).Initialize();
	}

	public virtual bool Equals(NetworkDataObject other) {
		return true;
	}

	public override int GetHashCode() => 0;

}

