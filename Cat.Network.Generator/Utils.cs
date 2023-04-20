using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network.Generator {
	internal static class Utils {

		private const string BinaryPrimitivesFQN = "System.Buffers.Binary.BinaryPrimitives";


		private static Dictionary<string, string> SerializationTemplates { get; } = new Dictionary<string, string>() {
			{ "System.Byte", $"{BinaryPrimitivesFQN}.WriteInt32LittleEndian({{1}}, 1); {{1}}.Slice(4)[0] = {{0}}"},
			{ "System.Int16", $"{BinaryPrimitivesFQN}.WriteInt32LittleEndian({{1}}, 2); {BinaryPrimitivesFQN}.WriteInt16LittleEndian({{1}}.Slice(4), {{0}}); {{1}} = {{1}}.Slice(4 + 2);"},
			{ "System.Int32", $"{BinaryPrimitivesFQN}.WriteInt32LittleEndian({{1}}, 4); {BinaryPrimitivesFQN}.WriteInt32LittleEndian({{1}}.Slice(4), {{0}}); {{1}} = {{1}}.Slice(4 + 4);"},
			{ "System.Int64", $"{BinaryPrimitivesFQN}.WriteInt32LittleEndian({{1}}, 8); {BinaryPrimitivesFQN}.WriteInt64LittleEndian({{1}}.Slice(4), {{0}}); {{1}} = {{1}}.Slice(4 + 8);"},
			{ "System.Boolean", $"{BinaryPrimitivesFQN}.WriteInt32LittleEndian({{1}}, 1); {{1}}.Slice(4)[0] = {{0}} ? (byte) 1 : (byte) 0; {{1}} = {{1}}.Slice(4 + 1);" }
		};
		
		private static Dictionary<string, string> DeserializationTemplates { get; } = new Dictionary<string, string>() {
			{ "System.Byte", "{0} = {1}[0];"},
			{ "System.Int16", $"{{0}} = {BinaryPrimitivesFQN}.ReadInt16LittleEndian({{1}});"},
			{ "System.Int32", $"{{0}} = {BinaryPrimitivesFQN}.ReadInt32LittleEndian({{1}});"},
			{ "System.Int64", $"{{0}} = {BinaryPrimitivesFQN}.ReadInt64LittleEndian({{1}});"},
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
