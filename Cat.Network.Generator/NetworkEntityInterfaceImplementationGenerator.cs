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
				System.Int32 propertyContentLength = buffer.Slice(8).Length - propertyContentBuffer.Length;
				{BinaryPrimitivesFQN}.WriteInt32LittleEndian(buffer.Slice(4), propertyContentLength);

				{SpanFQN} collectionContentBuffer = propertyContentBuffer.Slice(4);
			");
			
			foreach (NetworkCollectionData data in networkEntityClassDefinition.NetworkCollections) {
				writer.AppendLine(@$"
					if (serializationOptions.MemberSerializationMode == {MemberSerializationModeFQN}.Complete) {{
						{BinaryPrimitivesFQN}.WriteInt32LittleEndian(collectionContentBuffer, {data.Name}.Count + 1); collectionContentBuffer = collectionContentBuffer.Slice(4);
						collectionContentBuffer[0] = (System.Byte){NetworkCollectionOperationTypeFQN}.Clear; collectionContentBuffer = collectionContentBuffer.Slice(1);
						
						foreach (var item in {data.Name}) {{
							collectionContentBuffer[0] = (System.Byte){NetworkCollectionOperationTypeFQN}.Add; collectionContentBuffer = collectionContentBuffer.Slice(1);
							{GenerateSerialization(data.ItemSerializationExpression, "collectionContentBuffer")}
						}}
					}} else if (serializationOptions.MemberSerializationMode == {MemberSerializationModeFQN}.Partial) {{
						var operationBuffer = (({NetworkCollectionInterfaceFQN}<{data.ItemTypeInfo.FullyQualifiedTypeName}>){data.Name}).OperationBuffer;
						{BinaryPrimitivesFQN}.WriteInt32LittleEndian(collectionContentBuffer, operationBuffer.Count); collectionContentBuffer = collectionContentBuffer.Slice(4);

						foreach ({NetworkCollectionOperationFQN}<{data.ItemTypeInfo.FullyQualifiedTypeName}> operation in operationBuffer) {{
							var item = operation.Value;
							switch (operation.OperationType) {{
								case {NetworkCollectionOperationTypeFQN}.Add: {{
									collectionContentBuffer[0] = (System.Byte){NetworkCollectionOperationTypeFQN}.Add; collectionContentBuffer = collectionContentBuffer.Slice(1);
									{GenerateSerialization(data.ItemSerializationExpression, "collectionContentBuffer")}
									break;
								}}
								
								case {NetworkCollectionOperationTypeFQN}.Remove: {{
									collectionContentBuffer[0] = (System.Byte){NetworkCollectionOperationTypeFQN}.Remove; collectionContentBuffer = collectionContentBuffer.Slice(1);
									{BinaryPrimitivesFQN}.WriteInt32LittleEndian(collectionContentBuffer, operation.Index); collectionContentBuffer = collectionContentBuffer.Slice(4);
									break;
								}}
								case {NetworkCollectionOperationTypeFQN}.Set: {{
									collectionContentBuffer[0] = (System.Byte){NetworkCollectionOperationTypeFQN}.Set; collectionContentBuffer = collectionContentBuffer.Slice(1);	
									{BinaryPrimitivesFQN}.WriteInt32LittleEndian(collectionContentBuffer, operation.Index); collectionContentBuffer = collectionContentBuffer.Slice(4);
									{GenerateSerialization(data.ItemSerializationExpression, "collectionContentBuffer")}
									break;
								}}
								case {NetworkCollectionOperationTypeFQN}.Clear: {{
									collectionContentBuffer[0] = (System.Byte){NetworkCollectionOperationTypeFQN}.Clear; collectionContentBuffer = collectionContentBuffer.Slice(1);
									break;
								}}
							}}
						}}
					}}
				");
			}
			
			
		}
	}
}