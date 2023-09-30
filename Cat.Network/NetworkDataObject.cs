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

	public NetworkDataObject() {
		((INetworkSerializable)this).Initialize();
	}
	
}

