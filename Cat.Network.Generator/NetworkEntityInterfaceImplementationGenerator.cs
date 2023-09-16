using static Cat.Network.Generator.Utils;

namespace Cat.Network.Generator {
	public class NetworkEntityInterfaceImplementationGenerator : NetworkSerializableInterfaceImplementationGenerator {
		protected override void GenerateAdditionalInitialization(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition) {
			base.GenerateAdditionalInitialization(writer, classDefinition);
			
			NetworkEntityClassDefinition networkEntityClassDefinition = (NetworkEntityClassDefinition)classDefinition;
			
			foreach (NetworkCollectionData data in networkEntityClassDefinition.NetworkCollections) {
				writer.AppendLine($"(({NetworkCollectionPrefix})this).{data.BackingCollectionName} = new {NetworkValueListFQN}<{data.ItemTypeInfo.FullyQualifiedTypeName}>(this, (({NetworkCollectionPrefix})this).{data.Name});");
			}
		}

		protected override void GenerateAdditionalClean(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition) {
			base.GenerateAdditionalClean(writer, classDefinition);
			
			NetworkEntityClassDefinition networkEntityClassDefinition = (NetworkEntityClassDefinition)classDefinition;

			foreach (NetworkCollectionData collection in networkEntityClassDefinition.NetworkCollections) {
				writer.AppendLine($"(({NetworkCollectionInterfaceFQN}<{collection.ItemTypeInfo.FullyQualifiedTypeName}>){collection.Name}).OperationBuffer.Clear();");
			}
			
		}

		protected override void GenerateAdditionalSerialize(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition) {
			base.GenerateAdditionalSerialize(writer, classDefinition);
			
			NetworkEntityClassDefinition networkEntityClassDefinition = (NetworkEntityClassDefinition)classDefinition;
			
			writer.AppendBlock(@$"
				{SpanFQN} collectionContentLengthBuffer = contentBuffer;
				contentBuffer = contentBuffer.Slice(4);
			");
			
			foreach (NetworkCollectionData data in networkEntityClassDefinition.NetworkCollections) {
				writer.AppendBlock(@$"
					if (serializationOptions.MemberSerializationMode == {MemberSerializationModeFQN}.Complete) {{
						{BinaryPrimitivesFQN}.WriteInt32LittleEndian(contentBuffer, {data.Name}.Count + 1); contentBuffer = contentBuffer.Slice(4);
						contentBuffer[0] = (System.Byte){NetworkCollectionOperationTypeFQN}.Clear; contentBuffer = contentBuffer.Slice(1);
						
						foreach (var item in {data.Name}) {{
							contentBuffer[0] = (System.Byte){NetworkCollectionOperationTypeFQN}.Add; contentBuffer = contentBuffer.Slice(1);
							{GenerateSerialization(data.ItemSerializationExpression, "contentBuffer")}
						}}
					}} else if (serializationOptions.MemberSerializationMode == {MemberSerializationModeFQN}.Partial) {{
						var operationBuffer = (({NetworkCollectionInterfaceFQN}<{data.ItemTypeInfo.FullyQualifiedTypeName}>){data.Name}).OperationBuffer;
						{BinaryPrimitivesFQN}.WriteInt32LittleEndian(contentBuffer, operationBuffer.Count); contentBuffer = contentBuffer.Slice(4);

						foreach ({NetworkCollectionOperationFQN}<{data.ItemTypeInfo.FullyQualifiedTypeName}> operation in operationBuffer) {{
							var item = operation.Value;
							switch (operation.OperationType) {{
								case {NetworkCollectionOperationTypeFQN}.Add: {{
									contentBuffer[0] = (System.Byte){NetworkCollectionOperationTypeFQN}.Add; contentBuffer = contentBuffer.Slice(1);
									{GenerateSerialization(data.ItemSerializationExpression, "contentBuffer")}
									break;
								}}
								
								case {NetworkCollectionOperationTypeFQN}.Remove: {{
									contentBuffer[0] = (System.Byte){NetworkCollectionOperationTypeFQN}.Remove; contentBuffer = contentBuffer.Slice(1);
									{BinaryPrimitivesFQN}.WriteInt32LittleEndian(contentBuffer, operation.Index); contentBuffer = contentBuffer.Slice(4);
									break;
								}}
								case {NetworkCollectionOperationTypeFQN}.Set: {{
									contentBuffer[0] = (System.Byte){NetworkCollectionOperationTypeFQN}.Set; contentBuffer = contentBuffer.Slice(1);	
									{BinaryPrimitivesFQN}.WriteInt32LittleEndian(contentBuffer, operation.Index); contentBuffer = contentBuffer.Slice(4);
									{GenerateSerialization(data.ItemSerializationExpression, "contentBuffer")}
									break;
								}}
								case {NetworkCollectionOperationTypeFQN}.Clear: {{
									contentBuffer[0] = (System.Byte){NetworkCollectionOperationTypeFQN}.Clear; contentBuffer = contentBuffer.Slice(1);
									break;
								}}
							}}
						}}
					}}
				");
			}
			
			writer.AppendBlock($@"
				System.Int32 collectionContentLength = collectionContentLengthBuffer.Slice(4).Length - contentBuffer.Length;
				{BinaryPrimitivesFQN}.WriteInt32LittleEndian(collectionContentLengthBuffer, collectionContentLength);
			");
			
		}

		protected override void GenerateAdditionalDeserialize(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition) {
			base.GenerateAdditionalDeserialize(writer, classDefinition);
			
			NetworkEntityClassDefinition networkEntityClassDefinition = (NetworkEntityClassDefinition)classDefinition;
			
			foreach (NetworkCollectionData data in networkEntityClassDefinition.NetworkCollections) {
				writer.AppendBlock(@$"

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
			
		}
	}
}