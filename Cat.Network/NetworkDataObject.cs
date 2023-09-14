using System;

namespace Cat.Network;

public interface INetworkDataObject {
	
	INetworkSerializable Parent { get; set; }
	
}

public partial record NetworkDataObject : INetworkSerializable, INetworkDataObject {
	
	INetworkSerializable INetworkDataObject.Parent { get; set; }
	public ISerializationContext SerializationContext => ((INetworkDataObject)this).Parent?.SerializationContext;
	NetworkPropertyInfo[] INetworkSerializable.NetworkProperties { get; set; }

}