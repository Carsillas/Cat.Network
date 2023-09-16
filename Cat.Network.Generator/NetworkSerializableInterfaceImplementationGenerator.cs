using System;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using static Cat.Network.Generator.Utils;

// @formatter:csharp_max_line_length 400

namespace Cat.Network.Generator {
	public class NetworkSerializableInterfaceImplementationGenerator {
		public string GenerateNetworkSerializableSource(NetworkSerializableClassDefinition classDefinition) {
			ScopedStringWriter writer = new ScopedStringWriter();

			using (writer.EnterScope($"namespace {classDefinition.Namespace}")) {
				using (writer.EnterScope($"partial class {classDefinition.Name} : {NetworkEntityInterfaceFQN}")) {
					GenerateNetworkEntityInitializer(writer, classDefinition);
					GenerateNetworkCollectionClean(writer, classDefinition);
					GenerateNetworkEntitySerialize(writer, classDefinition);
					GenerateNetworkEntityDeserialize(writer, classDefinition);
				}
			}

			return writer.ToString();
		}


		protected virtual void GenerateAdditionalInitialization(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition) { }

		private void GenerateNetworkEntityInitializer(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition) {
			using (writer.EnterScope($"void {NetworkSerializableInterfaceFQN}.Initialize()")) {
				writer.AppendBlock($@"
					{NetworkPropertyInfoFQN}[] networkProperties = new {NetworkPropertyInfoFQN}[{classDefinition.NetworkProperties.Length}];
					{NetworkSerializableInterfaceFQN} iSerializable = this;
					iSerializable.NetworkProperties = networkProperties;
				");

				for (int i = 0; i < classDefinition.NetworkProperties.Length; i++) {
					NetworkPropertyData data = classDefinition.NetworkProperties[i];
					writer.AppendLine(
						$"networkProperties[{i}] = new {NetworkPropertyInfoFQN} {{ Index = {i}, Name = \"{data.Name}\" }};");
				}

				GenerateAdditionalInitialization(writer, classDefinition);
			}
		}

		protected virtual void GenerateAdditionalClean(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition) { }

		private void GenerateNetworkCollectionClean(ScopedStringWriter writer,
			NetworkSerializableClassDefinition classDefinition) {
			using (writer.EnterScope($"void {NetworkSerializableInterfaceFQN}.Clean()")) {
				GenerateAdditionalClean(writer, classDefinition);
			}
		}

		private static void GeneratePropertySerialize(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition) {
			using var scope = writer.EnterScope();

			writer.AppendBlock(@$"
				// Property serialization
				{SpanFQN} propertyContentLengthBuffer = contentBuffer;
				contentBuffer = contentBuffer.Slice(4);
			");

			for (int i = 0; i < classDefinition.NetworkProperties.Length; i++) {
				NetworkPropertyData data = classDefinition.NetworkProperties[i];

				writer.AppendBlock(@$"
					if (serializationOptions.MemberSelectionMode == {MemberSelectionModeFQN}.All || iSerializable.NetworkProperties[{i}].LastDirtyTick >= (iSerializable.SerializationContext?.Time ?? 0)) {{
						if (serializationOptions.MemberIdentifierMode == {MemberIdentifierModeFQN}.Name) {{
							 System.Int32 lengthStorage = {UnicodeFQN}.GetBytes(iSerializable.NetworkProperties[{i}].Name, contentBuffer.Slice(4)); {BinaryPrimitivesFQN}.WriteInt32LittleEndian(contentBuffer, lengthStorage); contentBuffer = contentBuffer.Slice(4 + lengthStorage);
						}}
						if (serializationOptions.MemberIdentifierMode == {MemberIdentifierModeFQN}.Index) {{
							{BinaryPrimitivesFQN}.WriteInt32LittleEndian(contentBuffer, {i}); contentBuffer = contentBuffer.Slice(4);
						}}

						{GenerateSerialization(data.SerializationExpression, "contentBuffer")}
					}}");
			}

			writer.AppendBlock($@"
				System.Int32 propertyContentLength = propertyContentLengthBuffer.Slice(4).Length - contentBuffer.Length;
				{BinaryPrimitivesFQN}.WriteInt32LittleEndian(propertyContentLengthBuffer, propertyContentLength);
			");
		}

		protected virtual void GenerateAdditionalSerialize(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition) { }

		private void GenerateNetworkEntitySerialize(ScopedStringWriter writer,
			NetworkSerializableClassDefinition classDefinition) {
			using (writer.EnterScope(
				       $"System.Int32 {NetworkSerializableInterfaceFQN}.Serialize({SerializationOptionsFQN} serializationOptions, {SpanFQN} buffer)")) {
				writer.AppendBlock($@"
					{SpanFQN} contentBuffer = buffer.Slice(4);
					{NetworkSerializableInterfaceFQN} iSerializable = this;
				");

				GeneratePropertySerialize(writer, classDefinition);

				GenerateAdditionalSerialize(writer, classDefinition);

				writer.AppendBlock(@$"
					System.Int32 contentLength = buffer.Slice(4).Length - contentBuffer.Length;
					{BinaryPrimitivesFQN}.WriteInt32LittleEndian(buffer, contentLength);
					return contentLength + 4;
				");
			}
		}

		private void GeneratePropertyDeserialize(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition) {
			using var scope = writer.EnterScope();

			writer.AppendBlock($@"
				{NetworkSerializableInterfaceFQN} iSerializable = this;
				
				System.Int32 propertyContentLength = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(buffer);
				System.Int32 collectionContentLength = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(buffer.Slice(propertyContentLength + 4));

				{ReadOnlySpanFQN} propertyContentBuffer = buffer.Slice(4, propertyContentLength);
				{ReadOnlySpanFQN} collectionContentBuffer = buffer.Slice(4 + propertyContentLength + 4, collectionContentLength);	
			");

			using (writer.EnterScope($"if (serializationOptions.MemberIdentifierMode == {MemberIdentifierModeFQN}.Name)")) { }

			using (writer.EnterScope($"if (serializationOptions.MemberIdentifierMode == {MemberIdentifierModeFQN}.Index)")) {
				using (writer.EnterScope($"while (propertyContentBuffer.Length > 0)")) {
					writer.AppendBlock($@"
						System.Int32 propertyIndex = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(propertyContentBuffer); propertyContentBuffer = propertyContentBuffer.Slice(4);
						System.Int32 propertyLength = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(propertyContentBuffer); propertyContentBuffer = propertyContentBuffer.Slice(4);
						ReadIndexedProperty(propertyIndex, propertyContentBuffer.Slice(0, propertyLength));
						iSerializable.NetworkProperties[propertyIndex].LastDirtyTick = iSerializable.SerializationContext.DeserializeDirtiesProperty ? iSerializable.SerializationContext?.Time ?? 0 : 0;
						propertyContentBuffer = propertyContentBuffer.Slice(propertyLength);
					");
				}

				using (writer.EnterScope($"void ReadIndexedProperty(System.Int32 index, {ReadOnlySpanFQN} indexedPropertyBuffer)")) {
					using (writer.EnterScope($"switch (index)")) {
						GenerateIndexedPropertyCases(classDefinition.NetworkProperties);
					}
				}
			}

			void GenerateIndexedPropertyCases(ImmutableArray<NetworkPropertyData> propertyDatas) {
				for (int i = 0; i < propertyDatas.Length; i++) {
					NetworkPropertyData data = propertyDatas[i];

					using (writer.EnterScope($"case {i}:")) {
						writer.AppendBlock(GenerateDeserialization(data.DeserializationExpression, "indexedPropertyBuffer"));
						writer.AppendLine("break;");
					}
				}
			}
		}

		protected virtual void GenerateAdditionalDeserialize(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition) { }

		private void GenerateNetworkEntityDeserialize(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition) {
			using (writer.EnterScope($"void {NetworkSerializableInterfaceFQN}.Deserialize({SerializationOptionsFQN} serializationOptions, {ReadOnlySpanFQN} buffer)")) {
				
				GeneratePropertyDeserialize(writer, classDefinition);
				
				GenerateAdditionalDeserialize(writer, classDefinition);
				
			}


		}
	}
}