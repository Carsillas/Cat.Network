using System;
using System.Collections.Generic;

namespace Cat.Network.Collections; 

public sealed class NetworkValueList<T> : NetworkList<T> {

	public NetworkValueList(NetworkEntity owner, List<T> list, bool fixedSize) : base(owner, list, fixedSize) { }

}