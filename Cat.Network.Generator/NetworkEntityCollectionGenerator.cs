using System;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using static Cat.Network.Generator.Utils;

namespace Cat.Network.Generator {
	public static class NetworkEntityCollectionGenerator {

		public static string GenerateNetworkCollectionSource(NetworkEntityClassDefinition classDefinition) {

			return $@"
namespace {classDefinition.Namespace} {{
	partial class {classDefinition.Name} : {classDefinition.Name}.{NetworkCollectionPrefix} {{



	}}
}}
";
		}


	}
}
