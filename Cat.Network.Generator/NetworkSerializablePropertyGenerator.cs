using System;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using static Cat.Network.Generator.Utils;

// @formatter:csharp_max_line_length 400

namespace Cat.Network.Generator {
	public abstract class NetworkSerializablePropertyGenerator {
		public string GenerateNetworkPropertySource(NetworkSerializableClassDefinition classDefinition) {
			ScopedStringWriter writer = new ScopedStringWriter();

			using (writer.EnterScope($"namespace {classDefinition.Namespace}")) {
				using (writer.EnterScope($"partial class {classDefinition.Name} : {NetworkEntityInterfaceFQN}, {classDefinition.Name}.{NetworkPropertyPrefix}")) {
					GenerateNetworkPropertyInterface(writer, classDefinition);
					GenerateNetworkPropertyDefinitions(writer, classDefinition);
				}
			}

			return writer.ToString();
		}


		private void GenerateNetworkPropertyInterface(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition) {
			bool isNetworkEntity = $"{classDefinition.Namespace}.{classDefinition.Name}" == NetworkEntityFQN;
			string superInterface = isNetworkEntity ? string.Empty : $" : {classDefinition.BaseTypeFQN}.{NetworkPropertyPrefix}";

			using (writer.EnterScope($"protected new interface {NetworkPropertyPrefix}{superInterface}")) {
				GenerateInterfaceProperties(writer, classDefinition);
			}
		}

		private void GenerateInterfaceProperties(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition) {
			foreach (NetworkPropertyData property in classDefinition.NetworkProperties.Where(property => property.Declared)) {
				writer.AppendLine(property.InterfacePropertyDeclaration);
			}
		}

		private void GenerateNetworkPropertyDefinitions(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition) {
			int declaredPropertiesStartIndex = classDefinition.NetworkProperties.Length - classDefinition.NetworkProperties.Count(property => property.Declared);

			int i = 0;
			foreach (NetworkPropertyData data in classDefinition.NetworkProperties.Where(property => property.Declared)) {
				int propertyIndex = declaredPropertiesStartIndex + i;
				
				GenerateAdditionalPropertyDefinition(writer, classDefinition, data);
				
				using (writer.EnterScope($"public {data.TypeInfo.FullyQualifiedTypeName} {data.Name}")) {
					GenerateGetter(writer, propertyIndex, data);
					GenerateSetter(writer, propertyIndex, data);
				}

				i++;
			}
		}

		protected virtual void GenerateAdditionalPropertyDefinition(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition, NetworkPropertyData data) { }

		protected abstract void GenerateGetter(ScopedStringWriter writer, int propertyIndex, NetworkPropertyData data);
		protected abstract void GenerateSetter(ScopedStringWriter writer, int propertyIndex, NetworkPropertyData data);
	}
}