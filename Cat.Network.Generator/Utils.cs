using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network.Generator {
	internal static class Utils {

		public const string BinaryPrimitivesFQN = "System.Buffers.Binary.BinaryPrimitives";
		public const string UnicodeFQN = "System.Text.Encoding.Unicode";
		public const string NetworkEntityInterfaceFQN = "Cat.Network.INetworkEntity";
		public const string NetworkPropertyInfoFQN = "Cat.Network.NetworkPropertyInfo";
		public const string SerializationOptionsFQN = "Cat.Network.SerializationOptions";
		public const string MemberIdentifierModeFQN = "Cat.Network.MemberIdentifierMode";
		public const string SpanFQN = "System.Span<byte>";
		public const string ReadOnlySpanFQN = "System.ReadOnlySpan<byte>";
		public const string NetworkEntityFQN = "Cat.Network.NetworkEntity";
		public const string NetworkPropertyPrefix = "NetworkProperty";
		public const string NetworkPropertyPrefixAndDot = NetworkPropertyPrefix + ".";
		public const string RPCPrefix = "RPC";
		public const string RPCPrefixAndDot = RPCPrefix + ".";


		private static Dictionary<string, string> SerializationTemplates { get; } = new Dictionary<string, string>() {
			{ "System.Byte", $"{BinaryPrimitivesFQN}.WriteInt32LittleEndian({{1}}, 1); {{1}}.Slice(4)[0] = {{0}}"},
			{ "System.Int16", $"{BinaryPrimitivesFQN}.WriteInt32LittleEndian({{1}}, 2); {BinaryPrimitivesFQN}.WriteInt16LittleEndian({{1}}.Slice(4), {{0}}); {{1}} = {{1}}.Slice(4 + 2);"},
			{ "System.Int32", $"{BinaryPrimitivesFQN}.WriteInt32LittleEndian({{1}}, 4); {BinaryPrimitivesFQN}.WriteInt32LittleEndian({{1}}.Slice(4), {{0}}); {{1}} = {{1}}.Slice(4 + 4);"},
			{ "System.Int64", $"{BinaryPrimitivesFQN}.WriteInt32LittleEndian({{1}}, 8); {BinaryPrimitivesFQN}.WriteInt64LittleEndian({{1}}.Slice(4), {{0}}); {{1}} = {{1}}.Slice(4 + 8);"},
			{ "System.UInt16", $"{BinaryPrimitivesFQN}.WriteInt32LittleEndian({{1}}, 2); {BinaryPrimitivesFQN}.WriteUInt16LittleEndian({{1}}.Slice(4), {{0}}); {{1}} = {{1}}.Slice(4 + 2);"},
			{ "System.UInt32", $"{BinaryPrimitivesFQN}.WriteInt32LittleEndian({{1}}, 4); {BinaryPrimitivesFQN}.WriteUInt32LittleEndian({{1}}.Slice(4), {{0}}); {{1}} = {{1}}.Slice(4 + 4);"},
			{ "System.UInt64", $"{BinaryPrimitivesFQN}.WriteInt32LittleEndian({{1}}, 8); {BinaryPrimitivesFQN}.WriteUInt64LittleEndian({{1}}.Slice(4), {{0}}); {{1}} = {{1}}.Slice(4 + 8);"},
			{ "System.Boolean", $"{BinaryPrimitivesFQN}.WriteInt32LittleEndian({{1}}, 1); {{1}}.Slice(4)[0] = {{0}} ? (byte) 1 : (byte) 0; {{1}} = {{1}}.Slice(4 + 1);" }
		};
		
		private static Dictionary<string, string> DeserializationTemplates { get; } = new Dictionary<string, string>() {
			{ "System.Byte", "{0} = {1}[0];"},
			{ "System.Int16", $"{{0}} = {BinaryPrimitivesFQN}.ReadInt16LittleEndian({{1}});"},
			{ "System.Int32", $"{{0}} = {BinaryPrimitivesFQN}.ReadInt32LittleEndian({{1}});"},
			{ "System.Int64", $"{{0}} = {BinaryPrimitivesFQN}.ReadInt64LittleEndian({{1}});"},
			{ "System.UInt16", $"{{0}} = {BinaryPrimitivesFQN}.ReadUInt16LittleEndian({{1}});"},
			{ "System.UInt32", $"{{0}} = {BinaryPrimitivesFQN}.ReadUInt32LittleEndian({{1}});"},
			{ "System.UInt64", $"{{0}} = {BinaryPrimitivesFQN}.ReadUInt64LittleEndian({{1}});"},
			{ "System.Boolean", "{0} = {1}[0] == 1;" }
		};


		public static string GenerateSerialization(string propertyName, string propertyTypeFQN, string bufferName) {

			string serializationExpression = "";

			if (SerializationTemplates.TryGetValue(propertyTypeFQN, out string template)) {
				serializationExpression = string.Format(template, propertyName, bufferName);
			}

			return serializationExpression;
		}

		public static string GenerateDeserialization(string propertyName, string propertyTypeFQN, string bufferName) {

			string deserializationExpression = "";

			if (DeserializationTemplates.TryGetValue(propertyTypeFQN, out string template)) {
				deserializationExpression = string.Format(template, propertyName, bufferName);
			}

			return deserializationExpression;
		}

	}
}
