using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using static Cat.Network.Generator.Utils;

namespace Cat.Network.Generator {
	public struct NetworkEntityClassDefinition {
		public string Name { get; set; }
		public string BaseTypeFQN { get; set; }
		public string Namespace { get; set; }
		public string MetadataName { get; set; }

		public bool IsNetworkEntity { get; set; }

		public ImmutableArray<NetworkPropertyData> NetworkProperties { get; set; }
		public ImmutableArray<NetworkPropertyData> DeclaredNetworkProperties { get; set; }
		public ImmutableArray<RPCMethodData> RPCs { get; set; }
		public ImmutableArray<RPCMethodData> DeclaredRPCs { get; set; }

		#region NetworkProperties

		public string GenerateNetworkPropertySource() {

			return $@"

namespace {Namespace} {{

	partial class {Name} : {NetworkEntityInterfaceFQN}, {Name}.{NetworkPropertyPrefix} {{

{GenerateNetworkPropertyInterface()}
{GenerateNetworkPropertyDefinitions()}
{GenerateNetworkPropertyInitializer()}
{GenerateNetworkPropertySerialize()}
{GenerateNetworkPropertyDeserialize()}
{GenerateNetworkPropertyClean()}

	}}
}}
";
		}


		private string GenerateNetworkPropertyInterface() {
			bool isNetworkEntity = $"{Namespace}.{Name}" == NetworkEntityFQN;
			string superInterface = isNetworkEntity ? "" : $": {BaseTypeFQN}.{NetworkPropertyPrefix}";
			string interfaceKeywords = isNetworkEntity ? "protected interface" : "protected new interface";
			return $@"
		protected interface {NetworkPropertyPrefix} {superInterface}{{
{GenerateInterfaceProperties()}
		}}
";
		}

		private string GenerateInterfaceProperties() {

			StringBuilder stringBuilder = new StringBuilder();

			foreach (NetworkPropertyData property in DeclaredNetworkProperties) {
				stringBuilder.AppendLine($"\t\t\t{property.InterfacePropertyDeclaration}");
			}

			return stringBuilder.ToString();

		}

		private string GenerateNetworkPropertyDefinitions() {

			StringBuilder stringBuilder = new StringBuilder();

			int declaredPropertiesStartIndex = NetworkProperties.Length - DeclaredNetworkProperties.Length;

			for (int i = 0; i < DeclaredNetworkProperties.Length; i++) {
				NetworkPropertyData data = DeclaredNetworkProperties[i];
				stringBuilder.AppendLine($"\t\t{data.AccessModifierText} {data.FullyQualifiedTypeName} {data.Name} {GenerateGetterSetter(declaredPropertiesStartIndex + i, data)}");
			}

			string GenerateGetterSetter(int propertyIndex, NetworkPropertyData data) {

				return
		$@" {{ 
			get => (({NetworkPropertyPrefix})this).{data.Name};
			set {{ 
				{NetworkEntityInterfaceFQN} iEntity = this;
				ref {NetworkPropertyInfoFQN} networkPropertyInfo = ref iEntity.NetworkProperties[{propertyIndex}];
				iEntity.LastDirtyTick = iEntity.SerializationContext?.Time ?? 0;
				networkPropertyInfo.Dirty = true;
				(({NetworkPropertyPrefix})this).{data.Name} = value; 
			}}
		}}";

			}

			return stringBuilder.ToString();
		}

		private string GenerateNetworkPropertyInitializer() {
			StringBuilder stringBuilder = new StringBuilder();

			stringBuilder.AppendLine($"\t\tvoid {NetworkEntityInterfaceFQN}.Initialize() {{");

			stringBuilder.AppendLine($"\t\t\t{NetworkPropertyInfoFQN}[] networkProperties = new {NetworkPropertyInfoFQN}[{NetworkProperties.Length}];");

			stringBuilder.AppendLine($"\t\t\t{NetworkEntityInterfaceFQN} iEntity = this;");
			stringBuilder.AppendLine($"\t\t\tiEntity.NetworkProperties = networkProperties;");
			for (int i = 0; i < NetworkProperties.Length; i++) {
				NetworkPropertyData data = NetworkProperties[i];
				stringBuilder.AppendLine($"\t\t\tnetworkProperties[{i}] = new {NetworkPropertyInfoFQN} {{ Index = {i}, Name = \"{data.Name}\" }};");
			}

			stringBuilder.AppendLine($"\t\t}}");

			return stringBuilder.ToString();
		}

		private string GenerateNetworkPropertySerialize() {
			StringBuilder stringBuilder = new StringBuilder();

			stringBuilder.AppendLine(@$"
		System.Int32 {NetworkEntityInterfaceFQN}.Serialize({SerializationOptionsFQN} serializationOptions, {SpanFQN} buffer) {{
			{SpanFQN} bufferCopy = buffer.Slice(4);
			System.Int32 lengthStorage = 0;
			{NetworkEntityInterfaceFQN} iEntity = this;
");


			for (int i = 0; i < NetworkProperties.Length; i++) {
				NetworkPropertyData data = NetworkProperties[i];


				stringBuilder.AppendLine($"\t\t\tif (iEntity.NetworkProperties[{i}].Dirty) {{");
				// TODO fix extra branching
				stringBuilder.AppendLine($"\t\t\t\tif (serializationOptions.MemberIdentifierMode == {MemberIdentifierModeFQN}.Name) {{");
				// TODO dont encode every time? might not matter since this is mainly for saving to disk
				stringBuilder.AppendLine($"\t\t\t\t\t lengthStorage = {UnicodeFQN}.GetBytes(iEntity.NetworkProperties[{i}].Name, bufferCopy.Slice(4)); {BinaryPrimitivesFQN}.WriteInt32LittleEndian(bufferCopy, lengthStorage); bufferCopy = bufferCopy.Slice(4 + lengthStorage);");
				stringBuilder.AppendLine($"\t\t\t\t}}");
				stringBuilder.AppendLine($"\t\t\t\tif (serializationOptions.MemberIdentifierMode == {MemberIdentifierModeFQN}.Index) {{");
				stringBuilder.AppendLine($"\t\t\t\t\t{BinaryPrimitivesFQN}.WriteInt32LittleEndian(bufferCopy, {i}); bufferCopy = bufferCopy.Slice(4);");
				stringBuilder.AppendLine($"\t\t\t\t}}");

				stringBuilder.AppendLine($"\t\t\t\t{GenerateSerialization(data.Name, data.FullyQualifiedTypeName, "bufferCopy")}");

				stringBuilder.AppendLine($"\t\t\t}}");
			}

			stringBuilder.AppendLine($"\t\t\tSystem.Int32 contentLength = buffer.Length - bufferCopy.Length;");
			stringBuilder.AppendLine($"\t\t\t{BinaryPrimitivesFQN}.WriteInt32LittleEndian(buffer, contentLength - 4);");
			stringBuilder.AppendLine($"\t\t\treturn contentLength;");
			stringBuilder.AppendLine($"\t\t}}");

			return stringBuilder.ToString();
		}

		private string GenerateNetworkPropertyDeserialize() {
			StringBuilder stringBuilder = new StringBuilder();

			stringBuilder.AppendLine($@"
		void {NetworkEntityInterfaceFQN}.Deserialize({SerializationOptionsFQN} serializationOptions, {ReadOnlySpanFQN} buffer) {{
			{ReadOnlySpanFQN} bufferCopy = buffer;
			{NetworkEntityInterfaceFQN} iEntity = this;

			if (serializationOptions.MemberIdentifierMode == {MemberIdentifierModeFQN}.Name) {{

			}}
			if (serializationOptions.MemberIdentifierMode == {MemberIdentifierModeFQN}.Index) {{
				while (bufferCopy.Length > 0) {{
					System.Int32 propertyIndex = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(bufferCopy); bufferCopy = bufferCopy.Slice(4);
					System.Int32 propertyLength = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(bufferCopy); bufferCopy = bufferCopy.Slice(4);
					ReadIndexedProperty(propertyIndex, bufferCopy.Slice(0, propertyLength));
					iEntity.NetworkProperties[propertyIndex].Dirty = iEntity.SerializationContext.DeserializeDirtiesProperty;
					bufferCopy = bufferCopy.Slice(propertyLength);
				}}

				void ReadIndexedProperty(System.Int32 index, {ReadOnlySpanFQN} propertyBuffer) {{
					switch (index) {{
{GenerateIndexedPropertyCases(NetworkProperties)}
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

		private string GenerateNetworkPropertyClean() {
			StringBuilder stringBuilder = new StringBuilder();

			stringBuilder.AppendLine($"\t\tvoid {NetworkEntityInterfaceFQN}.Clean() {{");

			stringBuilder.AppendLine($"\t\t}}");

			return stringBuilder.ToString();
		}

		#endregion

		#region RPCs

		public string GenerateRPCSource() {
			return $@"

namespace {Namespace} {{
	partial class {Name} : {NetworkEntityInterfaceFQN}, {Name}.{RPCPrefix} {{

{GenerateRPCInterface()}
{GenerateClassRPCs()}
{GenerateRPCHandler()}

	}}
}}
";
		}

		private string GenerateRPCInterface() {
			bool isNetworkEntity = $"{Namespace}.{this.Name}" == NetworkEntityFQN;
			string superInterface = isNetworkEntity ? "" : $": {BaseTypeFQN}.{RPCPrefix}";
			string interfaceKeywords = isNetworkEntity ? "protected interface" : "protected new interface";
			return $@"
		protected interface {RPCPrefix} {{
{GenerateInterfaceMethods()}
		}}
";
		}

		private string GenerateInterfaceMethods() {
			StringBuilder stringBuilder = new StringBuilder();

			foreach (RPCMethodData method in DeclaredRPCs) {
				stringBuilder.AppendLine($"\t\t\t{method.InterfaceMethodDeclaration};");
			}

			return stringBuilder.ToString();
		}

		private string GenerateRPCHandler() {
			StringBuilder stringBuilder = new StringBuilder();

			stringBuilder.AppendLine($@"
		void {NetworkEntityInterfaceFQN}.HandleRPCInvocation({NetworkEntityFQN} instigator, {ReadOnlySpanFQN} buffer) {{
			
			{ReadOnlySpanFQN} bufferCopy = buffer;
			
			int lengthStorage = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(bufferCopy);
			bufferCopy = bufferCopy.Slice(4);

			string methodName = {UnicodeFQN}.GetString(bufferCopy.Slice(0, lengthStorage));
			bufferCopy = bufferCopy.Slice(lengthStorage);
			switch (methodName) {{
{string.Join("\n", DeclaredRPCs.Select(rpc => GenerateCase(rpc)))}
			}}

		}}");
			
			return stringBuilder.ToString();

			string GenerateCase(RPCMethodData method) {

				return $@"
				case ""{method.InterfaceMethodDeclaration}"":
{string.Join("\n", method.Parameters.Select(parameter => $@"
					lengthStorage = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(bufferCopy);
					bufferCopy = bufferCopy.Slice(4);
					{parameter.FullyQualifiedTypeName} {GenerateDeserialization(parameter.ParameterName, parameter.FullyQualifiedTypeName, "bufferCopy.Slice(0, lengthStorage)")}
					bufferCopy = bufferCopy.Slice(lengthStorage);
"))}
					{method.ClassMethodInvocation};
					break;
";

			}

		}

		private string GenerateClassRPCs() {

			StringBuilder rpcStringBuilder = new StringBuilder();

			foreach (RPCMethodData method in DeclaredRPCs) {
				rpcStringBuilder.AppendLine($@"
		public {method.InterfaceMethodDeclaration} {{

			if (IsOwner) {{ 
				(({RPCPrefix})this).{method.ClassMethodInvocation};
			}} else {{

				var serializationContext = (({NetworkEntityInterfaceFQN})this).SerializationContext;
				{SpanFQN} buffer = serializationContext.RentRPCBuffer(this);
				{SpanFQN} bufferCopy = buffer.Slice(4);

{GenerateSerialization()}

			}}
		}}
");


				string GenerateSerialization() {
					StringBuilder serializationStringBuilder = new StringBuilder();

					serializationStringBuilder.AppendLine($"\t\t\t\tint lengthStorage;");
					serializationStringBuilder.AppendLine($"\t\t\t\tlengthStorage = {UnicodeFQN}.GetBytes(\"{method.InterfaceMethodDeclaration}\", bufferCopy.Slice(4)); {BinaryPrimitivesFQN}.WriteInt32LittleEndian(bufferCopy, lengthStorage); bufferCopy = bufferCopy.Slice(4 + lengthStorage);");

					foreach (RPCParameterData rpcParameterData in method.Parameters) {
						string serialization = Utils.GenerateSerialization(rpcParameterData.ParameterName, rpcParameterData.FullyQualifiedTypeName, "bufferCopy");

						serializationStringBuilder.AppendLine($"\t\t\t\t{serialization}");
					}

					serializationStringBuilder.AppendLine($"\t\t\t\tSystem.Int32 contentLength = buffer.Length - bufferCopy.Length;");
					serializationStringBuilder.AppendLine($"\t\t\t\t{BinaryPrimitivesFQN}.WriteInt32LittleEndian(buffer, contentLength - 4);");

					return serializationStringBuilder.ToString();
				}
			}

			return rpcStringBuilder.ToString();

		}

		#endregion

	}

}
