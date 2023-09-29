using System;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using static Cat.Network.Generator.Utils;

// @formatter:csharp_max_line_length 400

namespace Cat.Network.Generator {
	public static class NetworkEntityCollectionGenerator {
		public static string GenerateNetworkCollectionSource(NetworkEntityClassDefinition classDefinition) {
			ScopedStringWriter writer = new ScopedStringWriter();

			using (writer.EnterScope($"namespace {classDefinition.Namespace}")) {
				using (writer.EnterScope($"partial class {classDefinition.Name} : {classDefinition.Name}.{NetworkCollectionPrefix}")) {
					GenerateNetworkCollectionInterface(writer, classDefinition);
					GenerateNetworkCollectionDefinitions(writer, classDefinition);
				}
			}

			return writer.ToString();
		}


		private static void GenerateNetworkCollectionInterface(ScopedStringWriter writer, NetworkEntityClassDefinition classDefinition) {
			bool isNetworkEntity = $"{classDefinition.Namespace}.{classDefinition.Name}" == NetworkEntityFQN;
			string superInterface = isNetworkEntity ? "" : $": {classDefinition.BaseTypeFQN}.{NetworkCollectionPrefix} ";

			using (writer.EnterScope($"protected new interface {NetworkCollectionPrefix} {superInterface}")) {
				GenerateInterfaceCollections(writer, classDefinition);
			}
		}

		private static void GenerateInterfaceCollections(ScopedStringWriter writer, NetworkEntityClassDefinition classDefinition) {
			foreach (NetworkCollectionData collection in classDefinition.NetworkCollections.Where(collection => collection.Declared)) {
				writer.AppendLine(collection.InterfaceCollectionDeclaration);
				writer.AppendLine(collection.ExposedInterfaceCollectionDeclaration);
			}
		}


		private static void GenerateNetworkCollectionDefinitions(ScopedStringWriter writer, NetworkEntityClassDefinition classDefinition) {
			foreach (NetworkCollectionData data in classDefinition.NetworkCollections.Where(property => property.Declared)) {
				writer.AppendLine(data.ExposedExplicitInterfaceCollectionImplementation);
				writer.AppendLine(data.ExposedInterfaceCollectionImplementation);
			}
		}
	}
}