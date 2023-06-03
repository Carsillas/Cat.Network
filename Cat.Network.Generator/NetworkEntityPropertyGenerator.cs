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
{GenerateNetworkPropertyInitializer(classDefinition)}
{GenerateNetworkPropertySerialize(classDefinition)}
{GenerateNetworkPropertyDeserialize(classDefinition)}
{GenerateNetworkPropertyClean(classDefinition)}

	}}
}}
";
		}


		private static string GenerateNetworkPropertyInterface(NetworkEntityClassDefinition classDefinition) {
			bool isNetworkEntity = $"{classDefinition.Namespace}.{classDefinition.Name}" == NetworkEntityFQN;
			string superInterface = isNetworkEntity ? "" : $": {classDefinition.BaseTypeFQN}.{NetworkPropertyPrefix}";
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

		private static string GenerateNetworkPropertyInitializer(NetworkEntityClassDefinition classDefinition) {
			StringBuilder stringBuilder = new StringBuilder();

			stringBuilder.AppendLine($"\t\tvoid {NetworkEntityInterfaceFQN}.Initialize() {{");

			stringBuilder.AppendLine($"\t\t\t{NetworkPropertyInfoFQN}[] networkProperties = new {NetworkPropertyInfoFQN}[{classDefinition.NetworkProperties.Length}];");

			stringBuilder.AppendLine($"\t\t\t{NetworkEntityInterfaceFQN} iEntity = this;");
			stringBuilder.AppendLine($"\t\t\tiEntity.NetworkProperties = networkProperties;");
			for (int i = 0; i < classDefinition.NetworkProperties.Length; i++) {
				NetworkPropertyData data = classDefinition.NetworkProperties[i];
				stringBuilder.AppendLine($"\t\t\tnetworkProperties[{i}] = new {NetworkPropertyInfoFQN} {{ Index = {i}, Name = \"{data.Name}\" }};");
			}

			stringBuilder.AppendLine($"\t\t}}");

			return stringBuilder.ToString();
		}

		private static string GenerateNetworkPropertySerialize(NetworkEntityClassDefinition classDefinition) {
			StringBuilder stringBuilder = new StringBuilder();

			stringBuilder.AppendLine(@$"
		System.Int32 {NetworkEntityInterfaceFQN}.Serialize({SerializationOptionsFQN} serializationOptions, {SpanFQN} buffer) {{
			{SpanFQN} bufferCopy = buffer.Slice(4);
			System.Int32 lengthStorage = 0;
			{NetworkEntityInterfaceFQN} iEntity = this;
");


			for (int i = 0; i < classDefinition.NetworkProperties.Length; i++) {
				NetworkPropertyData data = classDefinition.NetworkProperties[i];


				stringBuilder.AppendLine(@$"
			if (iEntity.NetworkProperties[{i}].LastDirtyTick >= (iEntity.SerializationContext?.Time ?? 0)) {{
				if (serializationOptions.MemberIdentifierMode == {MemberIdentifierModeFQN}.Name) {{
					 lengthStorage = {UnicodeFQN}.GetBytes(iEntity.NetworkProperties[{i}].Name, bufferCopy.Slice(4)); {BinaryPrimitivesFQN}.WriteInt32LittleEndian(bufferCopy, lengthStorage); bufferCopy = bufferCopy.Slice(4 + lengthStorage);
				}}
				if (serializationOptions.MemberIdentifierMode == {MemberIdentifierModeFQN}.Index) {{
					{BinaryPrimitivesFQN}.WriteInt32LittleEndian(bufferCopy, {i}); bufferCopy = bufferCopy.Slice(4);
				}}

				{GenerateSerialization(data.Name, data.FullyQualifiedTypeName, "bufferCopy", "lengthStorage")}

			}}");
			}

			stringBuilder.AppendLine(@$"
			System.Int32 contentLength = buffer.Length - bufferCopy.Length;
			{BinaryPrimitivesFQN}.WriteInt32LittleEndian(buffer, contentLength - 4);
			return contentLength;
		}}");

			return stringBuilder.ToString();
		}

		private static string GenerateNetworkPropertyDeserialize(NetworkEntityClassDefinition classDefinition) {
			StringBuilder stringBuilder = new StringBuilder();

			stringBuilder.AppendLine($@"
		void {NetworkEntityInterfaceFQN}.Deserialize({SerializationOptionsFQN} serializationOptions, {ReadOnlySpanFQN} buffer) {{
			{ReadOnlySpanFQN} bufferCopy = buffer;
			{NetworkEntityInterfaceFQN} iEntity = this;

			System.Int32 lengthStorage = 0;

			if (serializationOptions.MemberIdentifierMode == {MemberIdentifierModeFQN}.Name) {{

			}}
			if (serializationOptions.MemberIdentifierMode == {MemberIdentifierModeFQN}.Index) {{
				while (bufferCopy.Length > 0) {{
					System.Int32 propertyIndex = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(bufferCopy); bufferCopy = bufferCopy.Slice(4);
					System.Int32 propertyLength = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(bufferCopy); bufferCopy = bufferCopy.Slice(4);
					ReadIndexedProperty(propertyIndex, bufferCopy.Slice(0, propertyLength));
					iEntity.NetworkProperties[propertyIndex].LastDirtyTick = iEntity.SerializationContext.DeserializeDirtiesProperty ? iEntity.SerializationContext?.Time ?? 0 : 0;
					bufferCopy = bufferCopy.Slice(propertyLength);
				}}

				void ReadIndexedProperty(System.Int32 index, {ReadOnlySpanFQN} propertyBuffer) {{
					switch (index) {{
{GenerateIndexedPropertyCases(classDefinition.NetworkProperties)}
					}}
				}}
			}}
		}}
");

			string GenerateIndexedPropertyCases(ImmutableArray<NetworkPropertyData> propertyDatas) {
				StringBuilder casesStringBuilder = new StringBuilder();
				for (int i = 0; i < propertyDatas.Length; i++) {
					NetworkPropertyData data = propertyDatas[i];

					casesStringBuilder.AppendLine($@"
						case {i}:
							{GenerateDeserialization(data.Name, data.FullyQualifiedTypeName, "propertyBuffer")}
							break;");
				}

				return casesStringBuilder.ToString();
			}


			return stringBuilder.ToString();
		}

		private static string GenerateNetworkPropertyClean(NetworkEntityClassDefinition classDefinition) {
			StringBuilder stringBuilder = new StringBuilder();

			stringBuilder.AppendLine($"\t\tvoid {NetworkEntityInterfaceFQN}.Clean() {{");

			stringBuilder.AppendLine($"\t\t}}");

			return stringBuilder.ToString();
		}

	}
}
