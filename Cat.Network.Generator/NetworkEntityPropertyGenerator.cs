using System;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using static Cat.Network.Generator.Utils;

namespace Cat.Network.Generator {
	public static class NetworkEntityPropertyGenerator {


		public static string GenerateNetworkPropertySource(NetworkEntityClassDefinition classDefinition) {

			return $@"

namespace {classDefinition.Namespace} {{

	partial class {classDefinition.Name} : {NetworkEntityInterfaceFQN}, {classDefinition.Name}.{NetworkPropertyPrefix} {{

{GenerateNetworkPropertyInterface(classDefinition)}
{GenerateNetworkPropertyDefinitions(classDefinition)}

	}}
}}
";
		}


		private static string GenerateNetworkPropertyInterface(NetworkEntityClassDefinition classDefinition) {
			bool isNetworkEntity = $"{classDefinition.Namespace}.{classDefinition.Name}" == NetworkEntityFQN;
			string superInterface = isNetworkEntity ? "" : $": {classDefinition.BaseTypeFQN}.{NetworkPropertyPrefix} ";
			string interfaceKeywords = isNetworkEntity ? "protected interface" : "protected new interface";
			return $@"
		{interfaceKeywords} {NetworkPropertyPrefix} {superInterface}{{
{GenerateInterfaceProperties(classDefinition)}
		}}
";
		}

		private static string GenerateInterfaceProperties(NetworkEntityClassDefinition classDefinition) {

			StringBuilder stringBuilder = new StringBuilder();

			foreach (NetworkPropertyData property in classDefinition.NetworkProperties.Where(property => property.Declared)) {
				stringBuilder.AppendLine($"\t\t\t{property.InterfacePropertyDeclaration}");
			}

			return stringBuilder.ToString();

		}

		private static string GenerateNetworkPropertyDefinitions(NetworkEntityClassDefinition classDefinition) {

			StringBuilder stringBuilder = new StringBuilder();

			int declaredPropertiesStartIndex = classDefinition.NetworkProperties.Length - classDefinition.NetworkProperties.Count(property => property.Declared);

			int i = 0;
			foreach (NetworkPropertyData data in classDefinition.NetworkProperties.Where(property => property.Declared)) {
				stringBuilder.AppendLine($"\t\t{data.AccessModifierText} {data.FullyQualifiedTypeName} {data.Name} {GenerateGetterSetter(declaredPropertiesStartIndex + i, data)}");
				i++;
			}

			string GenerateGetterSetter(int propertyIndex, NetworkPropertyData data) {

				return
		$@" {{ 
			get => (({NetworkPropertyPrefix})this).{data.Name};
			set {{ 
				{NetworkEntityInterfaceFQN} iEntity = this;
				ref {NetworkPropertyInfoFQN} networkPropertyInfo = ref iEntity.NetworkProperties[{propertyIndex}];
				iEntity.LastDirtyTick = iEntity.SerializationContext?.Time ?? 0;
				networkPropertyInfo.LastDirtyTick = iEntity.SerializationContext?.Time ?? 0;
				(({NetworkPropertyPrefix})this).{data.Name} = value; 
			}}
		}}";

			}

			return stringBuilder.ToString();
		}



	}
}
