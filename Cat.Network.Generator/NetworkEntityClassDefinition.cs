using System;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using static Cat.Network.Generator.Utils;

namespace Cat.Network.Generator {
	public partial struct NetworkEntityClassDefinition {
		public string Name { get; set; }
		public string BaseTypeFQN { get; set; }
		public string Namespace { get; set; }
		public string MetadataName { get; set; }

		public bool IsNetworkEntity { get; set; }

		public ImmutableArray<NetworkPropertyData> NetworkProperties { get; set; }
		public ImmutableArray<RPCMethodData> RPCs { get; set; }


	}

}
