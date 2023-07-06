using System;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using static Cat.Network.Generator.Utils;


namespace Cat.Network.Generator {
	public static class NetworkEntityRPCGenerator {

		public static string GenerateRPCSource(NetworkEntityClassDefinition classDefinition) {
			return $@"

namespace {classDefinition.Namespace} {{
	partial class {classDefinition.Name} : {NetworkEntityInterfaceFQN}, {classDefinition.Name}.{RPCPrefix} {{

{GenerateRPCInterface(classDefinition)}
{GenerateClassRPCs(classDefinition)}
{GenerateRPCHandler(classDefinition)}

	}}
}}
";
		}

		private static string GenerateRPCInterface(NetworkEntityClassDefinition classDefinition) {
			bool isNetworkEntity = $"{classDefinition.Namespace}.{classDefinition.Name}" == NetworkEntityFQN;
			string superInterface = isNetworkEntity ? "" : $": {classDefinition.BaseTypeFQN}.{RPCPrefix} ";
			string interfaceKeywords = isNetworkEntity ? "public interface" : "public new interface";
			return $@"
		{interfaceKeywords} {RPCPrefix} {superInterface}{{
{GenerateInterfaceMethods(classDefinition)}
		}}
";
		}

		private static string GenerateInterfaceMethods(NetworkEntityClassDefinition classDefinition) {
			StringBuilder stringBuilder = new StringBuilder();

			foreach (RPCMethodData method in classDefinition.RPCs.Where(rpc => rpc.Declared)) {
				stringBuilder.AppendLine($"\t\t\t{method.InterfaceMethodDeclaration};");
			}

			return stringBuilder.ToString();
		}

		private static string GenerateRPCHandler(NetworkEntityClassDefinition classDefinition) {
			StringBuilder stringBuilder = new StringBuilder();

			stringBuilder.AppendLine($@"
		void {NetworkEntityInterfaceFQN}.HandleRPCInvocation({NetworkEntityFQN} instigator, {ReadOnlySpanFQN} buffer) {{
			
			{ReadOnlySpanFQN} bufferCopy = buffer;
	
			System.Int64 methodNameHash = {BinaryPrimitivesFQN}.ReadInt64LittleEndian(bufferCopy.Slice(0, 8));
			bufferCopy = bufferCopy.Slice(8);
			switch (methodNameHash) {{
{string.Join("\n", classDefinition.RPCs.Select(rpc => GenerateCase(rpc)))}
			}}

		}}");

			return stringBuilder.ToString();

			string GenerateCase(RPCMethodData method) {

				using MD5 md5 = MD5.Create();

				byte[] hashBytes = md5.ComputeHash(Encoding.Unicode.GetBytes(method.InterfaceMethodDeclaration));
				long methodNameHashTruncated = BinaryPrimitives.ReadInt64LittleEndian(new ReadOnlySpan<byte>(hashBytes, 0, 8));

				return $@"
				case {methodNameHashTruncated}L: {{ // {method.InterfaceMethodDeclaration}
{string.Join("\n", method.Parameters.Select(parameter => $@"
						{parameter.TypeInfo.FullyQualifiedTypeName} {parameter.ParameterName} = default;
						{{
							System.Int32 lengthStorage = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(bufferCopy);
							bufferCopy = bufferCopy.Slice(4);
							{GenerateDeserialization(parameter.DeserializationExpression, "bufferCopy")}
							bufferCopy = bufferCopy.Slice(lengthStorage);
						}}
"))}
						{method.ClassMethodInvocation};
						break;
					}}
";

			}

		}

		private static string GenerateClassRPCs(NetworkEntityClassDefinition classDefinition) {

			StringBuilder rpcStringBuilder = new StringBuilder();

			foreach (RPCMethodData method in classDefinition.RPCs.Where(rpc => rpc.Declared)) {
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

					using MD5 md5 = MD5.Create();

					byte[] hashBytes = md5.ComputeHash(Encoding.Unicode.GetBytes(method.InterfaceMethodDeclaration));
					long methodNameHashTruncated = BinaryPrimitives.ReadInt64LittleEndian(new ReadOnlySpan<byte>(hashBytes, 0, 8));

					serializationStringBuilder.AppendLine($"\t\t\t\t{BinaryPrimitivesFQN}.WriteInt64LittleEndian(bufferCopy, {methodNameHashTruncated}L); bufferCopy = bufferCopy.Slice(8);");

					foreach (RPCParameterData rpcParameterData in method.Parameters) {
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
