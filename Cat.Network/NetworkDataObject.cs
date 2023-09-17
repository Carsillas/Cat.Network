using System;

namespace Cat.Network;

public interface INetworkDataObject : INetworkSerializable {
	int PropertyIndex { get; set; }
	INetworkSerializable Parent { get; set; }
	
}

public partial record NetworkDataObject : INetworkDataObject {
	
	int INetworkDataObject.PropertyIndex { get; set; }
	INetworkSerializable INetworkDataObject.Parent { get; set; }
	
	INetworkEntity INetworkSerializable.Anchor => ((INetworkDataObject)this).Parent?.Anchor;
	NetworkPropertyInfo[] INetworkSerializable.NetworkProperties { get; set; }

	public NetworkDataObject() {
		((INetworkSerializable)this).Initialize();
	}
	
}

