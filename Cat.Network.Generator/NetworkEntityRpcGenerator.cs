using System;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using static Cat.Network.Generator.Utils;


namespace Cat.Network.Generator {
	public static class NetworkEntityRpcGenerator {

		public static string GenerateRpcSource(NetworkEntityClassDefinition classDefinition) {
			return $@"

namespace {classDefinition.Namespace} {{
	partial class {classDefinition.Name} : {NetworkEntityInterfaceFQN}, {classDefinition.Name}.{RpcPrefix} {{

{GenerateRpcInterface(classDefinition)}
{GenerateClassRpcs(classDefinition)}
{GenerateRpcHandler(classDefinition)}
{GenerateRpcEvents(classDefinition)}

	}}
}}
";
		}


		private static string GenerateRpcInterface(NetworkEntityClassDefinition classDefinition) {
			bool isNetworkEntity = $"{classDefinition.Namespace}.{classDefinition.Name}" == NetworkEntityFQN;
			string superInterface = isNetworkEntity ? "" : $": {classDefinition.BaseTypeFQN}.{RpcPrefix} ";
			string interfaceKeywords = isNetworkEntity ? "public interface" : "public new interface";
			return $@"
		{interfaceKeywords} {RpcPrefix} {superInterface}{{
{GenerateInterfaceMethods(classDefinition)}
		}}
";
		}

		private static string GenerateRpcEvents(NetworkEntityClassDefinition classDefinition) {
			StringBuilder stringBuilder = new StringBuilder();

			foreach (RpcMethodData method in classDefinition.Rpcs.Where(rpc => rpc.Declared)) {
				if (method.IsAutoEvent) {
					stringBuilder.AppendLine($"\t\tpublic event {RpcPrefix}.{method.Name}Delegate On{method.Name};");
					stringBuilder.AppendLine($"\t\tvoid {RpcPrefix}.RaiseOn{method.Name}({method.GetDelegateDefinitionParameters(classDefinition)}) => On{method.Name}?.Invoke({method.DelegateInvocationParameters});");
				}
			}
			
			return stringBuilder.ToString();
		}
		
		
		private static string GenerateInterfaceMethods(NetworkEntityClassDefinition classDefinition) {
			StringBuilder stringBuilder = new StringBuilder();

			foreach (RpcMethodData method in classDefinition.Rpcs.Where(rpc => rpc.Declared)) {
				if (method.IsAutoEvent) {
					stringBuilder.AppendLine($"\t\t\tpublic delegate void {method.Name}Delegate({method.GetDelegateDefinitionParameters(classDefinition)});");
					stringBuilder.AppendLine($"\t\t\tevent {method.Name}Delegate On{method.Name};");
					stringBuilder.AppendLine($"\t\t\tvoid RaiseOn{method.Name}({method.GetDelegateDefinitionParameters(classDefinition)});");
				}
				stringBuilder.AppendLine($"\t\t\t{method.InterfaceMethodDeclaration};");
			}

			return stringBuilder.ToString();
		}

		private static string GenerateRpcHandler(NetworkEntityClassDefinition classDefinition) {
			StringBuilder stringBuilder = new StringBuilder();

			stringBuilder.AppendLine($@"
		void {NetworkEntityInterfaceFQN}.HandleRpcInvocation({GuidFQN} instigatorId, {ReadOnlySpanFQN} buffer) {{
			
			{SerializationOptionsFQN} serializationOptions = {CreateSerializationOptions};
			{ReadOnlySpanFQN} bufferCopy = buffer;
	
			System.Int64 methodNameHash = {BinaryPrimitivesFQN}.ReadInt64LittleEndian(bufferCopy.Slice(0, 8));
			bufferCopy = bufferCopy.Slice(8);
			switch (methodNameHash) {{
{string.Join("\n", classDefinition.Rpcs.Select(rpc => GenerateCase(rpc)))}
			}}

		}}");

			return stringBuilder.ToString();

			string GenerateCase(RpcMethodData method) {

				using MD5 md5 = MD5.Create();

				byte[] hashBytes = md5.ComputeHash(Encoding.Unicode.GetBytes(method.InterfaceMethodDeclaration));
				long methodNameHashTruncated = BinaryPrimitives.ReadInt64LittleEndian(new ReadOnlySpan<byte>(hashBytes, 0, 8));

				return $@"
				case {methodNameHashTruncated}L: {{ // {method.InterfaceMethodDeclaration}
{string.Join("\n", method.ClassParameters.Select(parameter => $@"
						{parameter.TypeInfo.FullyQualifiedTypeName} {parameter.Name} = default;
						{{
							System.Int32 lengthStorage = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(bufferCopy);
							bufferCopy = bufferCopy.Slice(4);
							{GenerateDeserialization(parameter.DeserializationExpression, "bufferCopy")}
							bufferCopy = bufferCopy.Slice(lengthStorage);
						}}
"))}
						(({RpcPrefix})this).{method.Name}({method.InterfaceMethodInvocationParameters});
						{(method.IsAutoEvent ? $"(({RpcPrefix})this).RaiseOn{method.Name}({method.DelegateInvocationParameters});" : string.Empty)}

						break;
					}}
";

			}

		}

		private static string GenerateClassRpcs(NetworkEntityClassDefinition classDefinition) {

			StringBuilder rpcStringBuilder = new StringBuilder();

			foreach (RpcMethodData method in classDefinition.Rpcs.Where(rpc => rpc.Declared)) {
				rpcStringBuilder.AppendLine($@"
		public {method.ClassMethodDeclaration} {{

			if (IsOwner) {{ 
				{GuidFQN} instigatorId = default;
				(({RpcPrefix})this).{method.Name}({method.InterfaceMethodInvocationParameters});
				{(method.IsAutoEvent ? $"(({RpcPrefix})this).RaiseOn{method.Name}({method.DelegateInvocationParameters});" : string.Empty)}
			}} else {{

				var serializationContext = (({NetworkEntityInterfaceFQN})this).SerializationContext;
				{SpanFQN} buffer = serializationContext.RentRpcBuffer(this);
				{SpanFQN} bufferCopy = buffer.Slice(4);

{GenerateSerialization()}

			}}
		}}
");


				string GenerateSerialization() {
					StringBuilder serializationStringBuilder = new StringBuilder();
					
					using MD5 md5 = MD5.Create();

					byte[] hashBytes = md5.ComputeHash(Encoding.Unicode.GetBytes(method.InterfaceMethodDeclaration));
					long methodNameHashTruncated = BinaryPrimitives.ReadInt64LittleEndian(new ReadOnlySpan<byte>(hashBytes, 0, 8));

					serializationStringBuilder.AppendLine($"\t\t\t\t{BinaryPrimitivesFQN}.WriteInt64LittleEndian(bufferCopy, {methodNameHashTruncated}L); bufferCopy = bufferCopy.Slice(8);");

					foreach (RpcParameterData rpcParameterData in method.ClassParameters) {
						string serialization = Utils.GenerateSerialization(rpcParameterData.SerializationExpression, "bufferCopy");

						serializationStringBuilder.AppendLine($"\t\t\t\t{serialization}");
					}

					serializationStringBuilder.AppendLine($"\t\t\t\tSystem.Int32 contentLength = buffer.Length - bufferCopy.Length;");
					serializationStringBuilder.AppendLine($"\t\t\t\t{BinaryPrimitivesFQN}.WriteInt32LittleEndian(buffer, contentLength - 4);");

					return serializationStringBuilder.ToString();
				}
			}

			return rpcStringBuilder.ToString();

		}

	}
}
