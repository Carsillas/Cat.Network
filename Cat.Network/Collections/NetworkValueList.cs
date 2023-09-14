using System.Collections.Generic;

namespace Cat.Network.Collections; 

public sealed class NetworkValueList<T> : NetworkList<T> where T : struct {
	
	public NetworkValueList(NetworkEntity owner, List<T> list) : base(owner, list) { }
	
}