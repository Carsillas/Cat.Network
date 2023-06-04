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

{GenerateNetworkCollectionInterface(classDefinition)}
{GenerateNetworkCollectionDefinitions(classDefinition)}

	}}
}}
";
		}


		private static string GenerateNetworkCollectionInterface(NetworkEntityClassDefinition classDefinition) {
			bool isNetworkEntity = $"{classDefinition.Namespace}.{classDefinition.Name}" == NetworkEntityFQN;
			string superInterface = isNetworkEntity ? "" : $": {classDefinition.BaseTypeFQN}.{NetworkCollectionPrefix} ";
			string interfaceKeywords = isNetworkEntity ? "protected interface" : "protected new interface";
			return $@"
		{interfaceKeywords} {NetworkCollectionPrefix} {superInterface}{{
{GenerateInterfaceCollections(classDefinition)}
		}}
";
		}

		private static string GenerateInterfaceCollections(NetworkEntityClassDefinition classDefinition) {

			StringBuilder stringBuilder = new StringBuilder();

			foreach (NetworkCollectionData collection in classDefinition.NetworkCollections.Where(collection => collection.Declared)) {
				stringBuilder.AppendLine($"\t\t\t{collection.InterfaceCollectionDeclaration}");
				stringBuilder.AppendLine($"\t\t\t{collection.ExposedInterfaceCollectionDeclaration}");
			}

			return stringBuilder.ToString();

		}


		private static string GenerateNetworkCollectionDefinitions(NetworkEntityClassDefinition classDefinition) {

			StringBuilder stringBuilder = new StringBuilder();

			int declaredPropertiesStartIndex = classDefinition.NetworkProperties.Length - classDefinition.NetworkProperties.Count(property => property.Declared);

			int i = 0;
			foreach (NetworkCollectionData data in classDefinition.NetworkCollections.Where(property => property.Declared)) {
				stringBuilder.AppendLine($"\t\t{data.ExposedExplicitInterfaceCollectionImplementation}");
				stringBuilder.AppendLine($"\t\t{data.ExposedInterfaceCollectionImplementation}");
				i++;
			}

			return stringBuilder.ToString();
		}

	}
}
