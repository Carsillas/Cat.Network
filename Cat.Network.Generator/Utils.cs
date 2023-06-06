using Microsoft.CodeAnalysis;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Cat.Network.Generator {
	internal static class Utils {

		public const string BinaryPrimitivesFQN = "System.Buffers.Binary.BinaryPrimitives";
		public const string UnicodeFQN = "System.Text.Encoding.Unicode";
		public const string NetworkEntityInterfaceFQN = "Cat.Network.INetworkEntity";
		public const string NetworkCollectionInterfaceFQN = "Cat.Network.INetworkCollection";
		public const string NetworkCollectionOperationFQN = "Cat.Network.NetworkCollectionOperation";
		public const string NetworkCollectionOperationTypeFQN = "Cat.Network.NetworkCollectionOperationType";
		public const string NetworkListFQN = "Cat.Network.NetworkList";
		public const string NetworkPropertyInfoFQN = "Cat.Network.NetworkPropertyInfo";
		public const string SerializationOptionsFQN = "Cat.Network.SerializationOptions";
		public const string MemberIdentifierModeFQN = "Cat.Network.MemberIdentifierMode";
		public const string MemberSerializationModeFQN = "Cat.Network.MemberSerializationMode";	
		public const string SpanFQN = "System.Span<byte>";
		public const string ReadOnlySpanFQN = "System.ReadOnlySpan<byte>";
		public const string NetworkEntityFQN = "Cat.Network.NetworkEntity";
		public const string NetworkPropertyPrefix = "NetworkProperty";
		public const string NetworkPropertyPrefixAndDot = NetworkPropertyPrefix + ".";
		public const string NetworkCollectionPrefix = "NetworkCollection";
		public const string NetworkCollectionPrefixAndDot = NetworkCollectionPrefix + ".";
		public const string RPCPrefix = "RPC";
		public const string RPCPrefixAndDot = RPCPrefix + ".";


		private const string PropertyBufferName = "propertyBuffer";

		private static Dictionary<string, string> SerializationTemplates { get; } = new Dictionary<string, string>() {
			{ "System.Byte", $"{PropertyBufferName}[0] = {{0}}; {PropertyBufferName} = {PropertyBufferName}.Slice(1);"},
			{ "System.Int16", $"{BinaryPrimitivesFQN}.WriteInt16LittleEndian({PropertyBufferName}, {{0}}); {PropertyBufferName} = {PropertyBufferName}.Slice(2);"},
			{ "System.Int32", $"{BinaryPrimitivesFQN}.WriteInt32LittleEndian({PropertyBufferName}, {{0}}); {PropertyBufferName} = {PropertyBufferName}.Slice(4);"},
			{ "System.Int64", $"{BinaryPrimitivesFQN}.WriteInt64LittleEndian({PropertyBufferName}, {{0}}); {PropertyBufferName} = {PropertyBufferName}.Slice(8);"},
			{ "System.UInt16", $"{BinaryPrimitivesFQN}.WriteUInt16LittleEndian({PropertyBufferName}, {{0}}); {PropertyBufferName} = {PropertyBufferName}.Slice(2);"},
			{ "System.UInt32", $"{BinaryPrimitivesFQN}.WriteUInt32LittleEndian({PropertyBufferName}, {{0}}); {PropertyBufferName} = {PropertyBufferName}.Slice(4);"},
			{ "System.UInt64", $"{BinaryPrimitivesFQN}.WriteUInt64LittleEndian({PropertyBufferName}, {{0}}); {PropertyBufferName} = {PropertyBufferName}.Slice(8);"},
			{ "System.Single", $"{BinaryPrimitivesFQN}.WriteSingleLittleEndian({PropertyBufferName}, {{0}}); {PropertyBufferName} = {PropertyBufferName}.Slice(4);"},
			{ "System.Double", $"{BinaryPrimitivesFQN}.WriteDoubleLittleEndian({PropertyBufferName}, {{0}}); {PropertyBufferName} = {PropertyBufferName}.Slice(8);"},
			{ "System.Boolean", $"{PropertyBufferName}[0] = {{0}} ? (byte) 1 : (byte) 0; {PropertyBufferName} = {PropertyBufferName}.Slice(1);" },
			{ "System.String", $"{PropertyBufferName} = {PropertyBufferName}.Slice({UnicodeFQN}.GetBytes({{0}}, {PropertyBufferName}));" }
		};
		
		private static Dictionary<string, string> DeserializationTemplates { get; } = new Dictionary<string, string>() {
			{ "System.Byte", $"{{0}} = {PropertyBufferName}[0];"},
			{ "System.Int16", $"{{0}} = {BinaryPrimitivesFQN}.ReadInt16LittleEndian({PropertyBufferName});"},
			{ "System.Int32", $"{{0}} = {BinaryPrimitivesFQN}.ReadInt32LittleEndian({PropertyBufferName});"},
			{ "System.Int64", $"{{0}} = {BinaryPrimitivesFQN}.ReadInt64LittleEndian({PropertyBufferName});"},
			{ "System.UInt16", $"{{0}} = {BinaryPrimitivesFQN}.ReadUInt16LittleEndian({PropertyBufferName});"},
			{ "System.UInt32", $"{{0}} = {BinaryPrimitivesFQN}.ReadUInt32LittleEndian({PropertyBufferName});"},
			{ "System.UInt64", $"{{0}} = {BinaryPrimitivesFQN}.ReadUInt64LittleEndian({PropertyBufferName});"},
			{ "System.Single", $"{{0}} = {BinaryPrimitivesFQN}.ReadSingleLittleEndian({PropertyBufferName});"},
			{ "System.Double", $"{{0}} = {BinaryPrimitivesFQN}.ReadDoubleLittleEndian({PropertyBufferName});"},
			{ "System.Boolean", $"{{0}} = {PropertyBufferName}[0] == 1;" },
			{ "System.String", $"{{0}} = {UnicodeFQN}.GetString({PropertyBufferName});" },
		};


		private static string SerializationTemplateWrapper = $"{{{{ {SpanFQN} {PropertyBufferName} = {{0}}.Slice(4); {{1}} System.Int32 lengthStorage = {{0}}.Slice(4).Length - {PropertyBufferName}.Length; {BinaryPrimitivesFQN}.WriteInt32LittleEndian({{0}}, lengthStorage); {{0}} = {{0}}.Slice(4 + lengthStorage); }}}}";
		private static string DeserializationTemplateWrapper = $"{{{{ {ReadOnlySpanFQN} {PropertyBufferName} = {{0}}; {{1}} }}}}";
		private static string NullableSerializationTemplate = $"{PropertyBufferName}[0] = {{0}}.HasValue ? (byte) 1 : (byte) 0; {PropertyBufferName} = {PropertyBufferName}.Slice(1); if ({{0}}.HasValue) {{{{ {{1}} }}}}";
		private static string NullableDeserializationTemplate = $"if ({PropertyBufferName}[0] == 1) {{{{ {PropertyBufferName} = {PropertyBufferName}.Slice(1); {{1}} }}}} else {{{{ {{0}} = null; }}}}";


		public static string GenerateSerialization(string propertyName, TypeInfo typeInfo, string bufferName) {
			
			string firstGenericArgument = typeInfo.GenericArgumentFQNs.Length >= 1 ? typeInfo.GenericArgumentFQNs[0] : "";
			string serializationTemplateType = typeInfo.IsNullable ? firstGenericArgument : typeInfo.FullyQualifiedTypeName;

			string serializationExpression = "";


			if (SerializationTemplates.TryGetValue(serializationTemplateType, out string template)) {
				string propertyNameSanitized = typeInfo.IsNullable ? $"{propertyName}.Value" : propertyName;

				serializationExpression = string.Format(template, propertyNameSanitized);
			} else {
				serializationExpression = $"/* Skipping serialization for unknown type: {typeInfo.FullyQualifiedTypeName} {propertyName} */";
			}

			if (typeInfo.IsNullable) {
				serializationExpression = string.Format(NullableSerializationTemplate, propertyName, serializationExpression);
			}

			serializationExpression = string.Format(SerializationTemplateWrapper, bufferName, serializationExpression);

			return serializationExpression;
		}

		public static string GenerateDeserialization(string propertyName, TypeInfo typeInfo, string bufferName) {

			string firstGenericArgument = typeInfo.GenericArgumentFQNs.Length >= 1 ? typeInfo.GenericArgumentFQNs[0] : "";
			string deserializationTemplateType = typeInfo.IsNullable ? firstGenericArgument : typeInfo.FullyQualifiedTypeName;

			string deserializationExpression = "";

			if (DeserializationTemplates.TryGetValue(deserializationTemplateType, out string template)) {
				deserializationExpression = string.Format(template, propertyName);
			} else {
				deserializationExpression = $"/* Skipping serialization for unknown type: {typeInfo.FullyQualifiedTypeName} {propertyName} */";
			}

			if (typeInfo.IsNullable) {
				deserializationExpression = string.Format(NullableDeserializationTemplate, propertyName, deserializationExpression);
			}

			deserializationExpression = string.Format(DeserializationTemplateWrapper, bufferName, deserializationExpression);

			return deserializationExpression;
		}

		public static bool PassNodesOfType<T>(SyntaxNode syntaxNode, CancellationToken cancellationToken) {
			if (syntaxNode is T) {
				return true;
			}
			return false;
		}

	}
}
