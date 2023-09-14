using System;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using static Cat.Network.Generator.Utils;

namespace Cat.Network.Generator {
	
	public class NetworkDataObjectDefinition : NetworkSerializableClassDefinition {
		public bool IsNetworkDataObject { get; set; }
	}

}
