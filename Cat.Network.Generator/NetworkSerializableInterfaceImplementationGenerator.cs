using System;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using static Cat.Network.Generator.Utils;

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

			writer.ToString();
		}


		protected virtual void GenerateAdditionalInitialization(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition) {
			
		}
		
		private void GenerateNetworkEntityInitializer(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition) {

			using (writer.EnterScope($"void {NetworkSerializableInterfaceFQN}.Initialize()")) {
				
				writer.AppendBlock($@"
					{NetworkPropertyInfoFQN}[] networkProperties = new {NetworkPropertyInfoFQN}[{classDefinition.NetworkProperties.Length}];
					{NetworkSerializableInterfaceFQN} iSerializable = this;
					iSerializable.NetworkProperties = networkProperties;
				");
				
				for (int i = 0; i < classDefinition.NetworkProperties.Length; i++) {
					NetworkPropertyData data = classDefinition.NetworkProperties[i];
					writer.AppendLine($"networkProperties[{i}] = new {NetworkPropertyInfoFQN} {{ Index = {i}, Name = \"{data.Name}\" }};");
				}

				GenerateAdditionalInitialization(writer, classDefinition);
			}

		}

		protected virtual void GenerateAdditionalClean(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition) {
			
		}
		
		private void GenerateNetworkCollectionClean(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition) {
			using (writer.EnterScope($"void {NetworkSerializableInterfaceFQN}.Clean()")) {
				GenerateAdditionalClean(writer, classDefinition);
			}
		}

			
		protected virtual void GenerateAdditionalSerialize(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition) {
			
		}
		
		private void GenerateNetworkEntitySerialize(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition) {

			using (writer.EnterScope(
				       $"System.Int32 {NetworkSerializableInterfaceFQN}.Serialize({SerializationOptionsFQN} serializationOptions, {SpanFQN} buffer)")) {
				
				// TODO change propertyContentBuffer to just contentBuffer
				
				writer.AppendBlock($@"
					{SpanFQN} propertyContentBuffer = buffer.Slice(8);
					{NetworkSerializableInterfaceFQN} iSerializable = this;
				");
				
				for (int i = 0; i < classDefinition.NetworkProperties.Length; i++) {
					NetworkPropertyData data = classDefinition.NetworkProperties[i];

					writer.AppendBlock(@$"
					if (serializationOptions.MemberSelectionMode == {MemberSelectionModeFQN}.All || iSerializable.NetworkProperties[{i}].LastDirtyTick >= (iSerializable.SerializationContext?.Time ?? 0)) {{
						if (serializationOptions.MemberIdentifierMode == {MemberIdentifierModeFQN}.Name) {{
							 System.Int32 lengthStorage = {UnicodeFQN}.GetBytes(iSerializable.NetworkProperties[{i}].Name, propertyContentBuffer.Slice(4)); {BinaryPrimitivesFQN}.WriteInt32LittleEndian(propertyContentBuffer, lengthStorage); propertyContentBuffer = propertyContentBuffer.Slice(4 + lengthStorage);
						}}
						if (serializationOptions.MemberIdentifierMode == {MemberIdentifierModeFQN}.Index) {{
							{BinaryPrimitivesFQN}.WriteInt32LittleEndian(propertyContentBuffer, {i}); propertyContentBuffer = propertyContentBuffer.Slice(4);
						}}

						{GenerateSerialization(data.SerializationExpression, "propertyContentBuffer")}
					}}");
				}
				
				GenerateAdditionalSerialize(writer, classDefinition);
				
				writer.AppendBlock(@$"
					System.Int32 collectionContentLength = propertyContentBuffer.Slice(4).Length - collectionContentBuffer.Length;
					{BinaryPrimitivesFQN}.WriteInt32LittleEndian(propertyContentBuffer, collectionContentLength);
					System.Int32 contentLength = buffer.Slice(4).Length - collectionContentBuffer.Length;
					{BinaryPrimitivesFQN}.WriteInt32LittleEndian(buffer, contentLength);
					return contentLength + 4;
				");
				
			}



			return stringBuilder.ToString();
		}


		private void GenerateNetworkEntityDeserialize(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition) {
			StringBuilder stringBuilder = new StringBuilder();

			stringBuilder.AppendLine($@"
		void {NetworkSerializableInterfaceFQN}.Deserialize({SerializationOptionsFQN} serializationOptions, {ReadOnlySpanFQN} buffer) {{

			{NetworkSerializableInterfaceFQN} iSerializable = this;
			
			System.Int32 propertyContentLength = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(buffer);
			System.Int32 collectionContentLength = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(buffer.Slice(propertyContentLength + 4));

			{ReadOnlySpanFQN} propertyContentBuffer = buffer.Slice(4, propertyContentLength);
			{ReadOnlySpanFQN} collectionContentBuffer = buffer.Slice(4 + propertyContentLength + 4, collectionContentLength);

			if (serializationOptions.MemberIdentifierMode == {MemberIdentifierModeFQN}.Name) {{

			}}
			if (serializationOptions.MemberIdentifierMode == {MemberIdentifierModeFQN}.Index) {{
				while (propertyContentBuffer.Length > 0) {{
					System.Int32 propertyIndex = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(propertyContentBuffer); propertyContentBuffer = propertyContentBuffer.Slice(4);
					System.Int32 propertyLength = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(propertyContentBuffer); propertyContentBuffer = propertyContentBuffer.Slice(4);
					ReadIndexedProperty(propertyIndex, propertyContentBuffer.Slice(0, propertyLength));
					iSerializable.NetworkProperties[propertyIndex].LastDirtyTick = iSerializable.SerializationContext.DeserializeDirtiesProperty ? iSerializable.SerializationContext?.Time ?? 0 : 0;
					propertyContentBuffer = propertyContentBuffer.Slice(propertyLength);
				}}

				void ReadIndexedProperty(System.Int32 index, {ReadOnlySpanFQN} indexedPropertyBuffer) {{
					switch (index) {{
{GenerateIndexedPropertyCases(classDefinition.NetworkProperties)}
					}}
				}}
			}}");


			foreach (NetworkCollectionData data in classDefinition.NetworkCollections) {
				stringBuilder.AppendLine(@$"

			System.Int32 operationCount = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(collectionContentBuffer); collectionContentBuffer = collectionContentBuffer.Slice(4);
			
			var operationBuffer = (({NetworkCollectionInterfaceFQN}<{data.ItemTypeInfo.FullyQualifiedTypeName}>){data.Name}).OperationBuffer;

			for (System.Int32 i = 0; i < operationCount; i++) {{
				
				{NetworkCollectionOperationTypeFQN} operationType = ({NetworkCollectionOperationTypeFQN})collectionContentBuffer[0]; collectionContentBuffer = collectionContentBuffer.Slice(1);

				switch (operationType) {{
					case {NetworkCollectionOperationTypeFQN}.Add: {{
						System.Int32 itemLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(collectionContentBuffer); collectionContentBuffer = collectionContentBuffer.Slice(4);
						
						{data.ItemTypeInfo.FullyQualifiedTypeName} item;
						{GenerateDeserialization(data.ItemDeserializationExpression, "collectionContentBuffer")}

						collectionContentBuffer = collectionContentBuffer.Slice(itemLength);

						if (iSerializable.SerializationContext.DeserializeDirtiesProperty) {{
							iSerializable.SerializationContext.MarkForClean(iSerializable.Anchor);
							operationBuffer.Add(new {NetworkCollectionOperationFQN}<{data.ItemTypeInfo.FullyQualifiedTypeName}> {{
								OperationType = {NetworkCollectionOperationTypeFQN}.Add,
								Value = item
							}});
						}}
						
						(({NetworkCollectionPrefix})this).{data.Name}.Add(item);
						break;
					}}
						
					case {NetworkCollectionOperationTypeFQN}.Remove: {{
						System.Int32 index = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(collectionContentBuffer); collectionContentBuffer = collectionContentBuffer.Slice(4);
						
						if (iSerializable.SerializationContext.DeserializeDirtiesProperty) {{
							iSerializable.SerializationContext.MarkForClean(iSerializable.Anchor);
							operationBuffer.Add(new {NetworkCollectionOperationFQN}<{data.ItemTypeInfo.FullyQualifiedTypeName}> {{
								OperationType = {NetworkCollectionOperationTypeFQN}.Remove,
								Index = index
							}});
						}}

						(({NetworkCollectionPrefix})this).{data.Name}.RemoveAt(index);
						break;
					}}
					case {NetworkCollectionOperationTypeFQN}.Set: {{
						System.Int32 index = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(collectionContentBuffer); collectionContentBuffer = collectionContentBuffer.Slice(4);
						System.Int32 itemLength = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(collectionContentBuffer); collectionContentBuffer = collectionContentBuffer.Slice(4);
						
						{data.ItemTypeInfo.FullyQualifiedTypeName} item;
						{GenerateDeserialization(data.ItemDeserializationExpression, "collectionContentBuffer")}
						
						collectionContentBuffer = collectionContentBuffer.Slice(itemLength);

						if (iSerializable.SerializationContext.DeserializeDirtiesProperty) {{
							iSerializable.SerializationContext.MarkForClean(iSerializable.Anchor);
							operationBuffer.Add(new {NetworkCollectionOperationFQN}<{data.ItemTypeInfo.FullyQualifiedTypeName}> {{
								OperationType = {NetworkCollectionOperationTypeFQN}.Set,
								Index = index,
								Value = item
							}});
						}}

						(({NetworkCollectionPrefix})this).{data.Name}[index] = item;

						break;
					}}
					case {NetworkCollectionOperationTypeFQN}.Clear: {{

						if (iSerializable.SerializationContext.DeserializeDirtiesProperty) {{
							iSerializable.SerializationContext.MarkForClean(iSerializable.Anchor);
							operationBuffer.Add(new {NetworkCollectionOperationFQN}<{data.ItemTypeInfo.FullyQualifiedTypeName}> {{
								OperationType = {NetworkCollectionOperationTypeFQN}.Clear
							}});
						}}

						(({NetworkCollectionPrefix})this).{data.Name}.Clear();
						break;
					}}
				}}
			}}
");
			}

			stringBuilder.AppendLine($@"
		}}");

			string GenerateIndexedPropertyCases(ImmutableArray<NetworkPropertyData> propertyDatas) {
				StringBuilder casesStringBuilder = new StringBuilder();
				for (int i = 0; i < propertyDatas.Length; i++) {
					NetworkPropertyData data = propertyDatas[i];

					casesStringBuilder.AppendLine($@"
						case {i}:
							{GenerateDeserialization(data.DeserializationExpression, "indexedPropertyBuffer")}
							break;");
				}

				return casesStringBuilder.ToString();
			}


			return stringBuilder.ToString();
		}

	}
}
