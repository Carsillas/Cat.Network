using System;

namespace Cat.Network;

public interface INetworkDataObject {
	
	INetworkSerializable Parent { get; set; }
	
}

public partial record NetworkDataObject : INetworkSerializable, INetworkDataObject {
	
	INetworkSerializable INetworkDataObject.Parent { get; set; }
	INetworkEntity INetworkSerializable.Anchor => ((INetworkDataObject)this).Parent?.Anchor;
	NetworkPropertyInfo[] INetworkSerializable.NetworkProperties { get; set; }

	public NetworkDataObject() {
		((INetworkSerializable)this).Initialize();
	}
	
}

