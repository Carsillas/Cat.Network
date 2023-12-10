using System;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using static Cat.Network.Generator.Utils;

namespace Cat.Network.Generator {
	public class NetworkEntityClassDefinition : NetworkSerializableClassDefinition {
		public bool IsNetworkEntity { get; set; }

		public ImmutableArray<NetworkCollectionData> NetworkCollections { get; set; }
		public ImmutableArray<RpcMethodData> Rpcs { get; set; }


	}

}
