using static Cat.Network.Generator.Utils;

// @formatter:csharp_max_line_length 400

namespace Cat.Network.Generator {
	public class NetworkEntityInterfaceImplementationGenerator : NetworkSerializableInterfaceImplementationGenerator {
		protected override string SerializableTypeKind { get; } = "class";

		protected override string InterfaceFQN { get; } = NetworkEntityInterfaceFQN;

		protected override void GenerateAdditionalInitialization(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition) {
			base.GenerateAdditionalInitialization(writer, classDefinition);

			NetworkEntityClassDefinition networkEntityClassDefinition = (NetworkEntityClassDefinition)classDefinition;

			foreach (NetworkCollectionData data in networkEntityClassDefinition.NetworkCollections) {
				writer.AppendLine($"(({NetworkCollectionPrefix})this).{data.BackingCollectionName} = new {data.NetworkListTypeFQN}<{data.ItemTypeInfo.FullyQualifiedTypeName}>(this, (({NetworkCollectionPrefix})this).{data.Name});");
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
				using (writer.EnterScope($"if (serializationOptions.MemberSerializationMode == {MemberSerializationModeFQN}.Complete)")) {
					writer.AppendBlock($@"
						{BinaryPrimitivesFQN}.WriteInt32LittleEndian(contentBuffer, {data.Name}.Count + 1); contentBuffer = contentBuffer.Slice(4);
						contentBuffer[0] = (System.Byte){NetworkCollectionOperationTypeFQN}.Clear; contentBuffer = contentBuffer.Slice(1);
						
						foreach (var item in {data.Name}) {{
							contentBuffer[0] = (System.Byte){NetworkCollectionOperationTypeFQN}.Add; contentBuffer = contentBuffer.Slice(1);
							{GenerateSerialization(data.CompleteItemSerializationExpression, "contentBuffer")}
						}}
					");
				}

				using (writer.EnterScope($"else if (serializationOptions.MemberSerializationMode == {MemberSerializationModeFQN}.Partial)")) {
					writer.AppendBlock($@"
						var operationBuffer = (({NetworkCollectionInterfaceFQN}<{data.ItemTypeInfo.FullyQualifiedTypeName}>){data.Name}).OperationBuffer;
						{BinaryPrimitivesFQN}.WriteInt32LittleEndian(contentBuffer, operationBuffer.Count); contentBuffer = contentBuffer.Slice(4);

						foreach ({NetworkCollectionOperationFQN}<{data.ItemTypeInfo.FullyQualifiedTypeName}> operation in operationBuffer) {{
							var item = operation.Value;
							switch (operation.OperationType) {{
								case {NetworkCollectionOperationTypeFQN}.Add: {{
									contentBuffer[0] = (System.Byte){NetworkCollectionOperationTypeFQN}.Add; contentBuffer = contentBuffer.Slice(1);
									{GenerateSerialization(data.CompleteItemSerializationExpression, "contentBuffer")}
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
									{GenerateSerialization(data.CompleteItemSerializationExpression, "contentBuffer")}
									break;
								}}
								case {NetworkCollectionOperationTypeFQN}.Swap: {{
									contentBuffer[0] = (System.Byte){NetworkCollectionOperationTypeFQN}.Swap; contentBuffer = contentBuffer.Slice(1);	
									{BinaryPrimitivesFQN}.WriteInt32LittleEndian(contentBuffer, operation.Index); contentBuffer = contentBuffer.Slice(4);
									{BinaryPrimitivesFQN}.WriteInt32LittleEndian(contentBuffer, operation.SwapIndex); contentBuffer = contentBuffer.Slice(4);
									break;
								}}
								case {NetworkCollectionOperationTypeFQN}.Update: {{
									contentBuffer[0] = (System.Byte){NetworkCollectionOperationTypeFQN}.Update; contentBuffer = contentBuffer.Slice(1);	
									{BinaryPrimitivesFQN}.WriteInt32LittleEndian(contentBuffer, operation.Index); contentBuffer = contentBuffer.Slice(4);
									{GenerateSerialization(data.PartialItemSerializationExpression, "contentBuffer")}
									break;
								}}
								case {NetworkCollectionOperationTypeFQN}.Clear: {{
									contentBuffer[0] = (System.Byte){NetworkCollectionOperationTypeFQN}.Clear; contentBuffer = contentBuffer.Slice(1);
									break;
								}}
							}}
						}}
					");
				}
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
				writer.AppendBlock($@"
					System.Int32 collectionContentLength = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(contentBuffer);
					{ReadOnlySpanFQN} collectionContentBuffer = contentBuffer.Slice(4, collectionContentLength);
					contentBuffer = contentBuffer.Slice(collectionContentLength);

					System.Int32 operationCount = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(collectionContentBuffer); collectionContentBuffer = collectionContentBuffer.Slice(4);

					var operationBuffer = (({NetworkCollectionInterfaceFQN}<{data.ItemTypeInfo.FullyQualifiedTypeName}>){data.Name}).OperationBuffer;
				");

				using (writer.EnterScope("for (System.Int32 i = 0; i < operationCount; i++)")) {
					writer.AppendLine($"{NetworkCollectionOperationTypeFQN} operationType = ({NetworkCollectionOperationTypeFQN})collectionContentBuffer[0]; collectionContentBuffer = collectionContentBuffer.Slice(1);");

					using (writer.EnterScope("switch (operationType)")) {
						writer.AppendBlock($@"
							case {NetworkCollectionOperationTypeFQN}.Add: {{
								System.Int32 itemLength = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(collectionContentBuffer); collectionContentBuffer = collectionContentBuffer.Slice(4);
								
								{data.ItemTypeInfo.FullyQualifiedTypeName} item = default;
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
						");

						writer.AppendBlock($@"
							case {NetworkCollectionOperationTypeFQN}.Remove: {{
								System.Int32 index = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(collectionContentBuffer); collectionContentBuffer = collectionContentBuffer.Slice(4);

								if (iSerializable.SerializationContext?.DeserializeDirtiesProperty == true) {{
									iSerializable.SerializationContext.MarkForClean(iSerializable.Anchor);
									operationBuffer.Add(new {NetworkCollectionOperationFQN}<{data.ItemTypeInfo.FullyQualifiedTypeName}> {{
										OperationType = {NetworkCollectionOperationTypeFQN}.Remove,
										Index = index
									}});
								}}

								(({NetworkCollectionPrefix})this).{data.Name}.RemoveAt(index);
								break;
							}}
						");

						writer.AppendBlock($@"
							case {NetworkCollectionOperationTypeFQN}.Set: {{
								System.Int32 index = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(collectionContentBuffer); collectionContentBuffer = collectionContentBuffer.Slice(4);
								System.Int32 itemLength = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(collectionContentBuffer); collectionContentBuffer = collectionContentBuffer.Slice(4);

								{data.ItemTypeInfo.FullyQualifiedTypeName} item = default;
								{GenerateDeserialization(data.ItemDeserializationExpression, "collectionContentBuffer")}

								collectionContentBuffer = collectionContentBuffer.Slice(itemLength);

								if (iSerializable.SerializationContext?.DeserializeDirtiesProperty == true) {{
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
						");
						
						writer.AppendBlock($@"
							case {NetworkCollectionOperationTypeFQN}.Swap: {{
								System.Int32 index = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(collectionContentBuffer); collectionContentBuffer = collectionContentBuffer.Slice(4);
								System.Int32 swapIndex = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(collectionContentBuffer); collectionContentBuffer = collectionContentBuffer.Slice(4);

								if (iSerializable.SerializationContext?.DeserializeDirtiesProperty == true) {{
									iSerializable.SerializationContext.MarkForClean(iSerializable.Anchor);
									operationBuffer.Add(new {NetworkCollectionOperationFQN}<{data.ItemTypeInfo.FullyQualifiedTypeName}> {{
										OperationType = {NetworkCollectionOperationTypeFQN}.Swap,
										Index = index,
										SwapIndex = swapIndex
									}});
								}}

								{data.ItemTypeInfo.FullyQualifiedTypeName} item = (({NetworkCollectionPrefix})this).{data.Name}[index];
								(({NetworkCollectionPrefix})this).{data.Name}[index] = (({NetworkCollectionPrefix})this).{data.Name}[swapIndex];
								(({NetworkCollectionPrefix})this).{data.Name}[swapIndex] = item;
								break;
							}}
						");
						
						writer.AppendBlock($@"
							case {NetworkCollectionOperationTypeFQN}.Clear: {{

								if (iSerializable.SerializationContext?.DeserializeDirtiesProperty == true) {{
									iSerializable.SerializationContext.MarkForClean(iSerializable.Anchor);
									operationBuffer.Add(new {NetworkCollectionOperationFQN}<{data.ItemTypeInfo.FullyQualifiedTypeName}> {{
										OperationType = {NetworkCollectionOperationTypeFQN}.Clear
									}});
								}}

								(({NetworkCollectionPrefix})this).{data.Name}.Clear();
								break;
							}}
						");

						writer.AppendBlock($@"
							case {NetworkCollectionOperationTypeFQN}.Update: {{
								System.Int32 index = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(collectionContentBuffer); collectionContentBuffer = collectionContentBuffer.Slice(4);
								System.Int32 itemLength = {BinaryPrimitivesFQN}.ReadInt32LittleEndian(collectionContentBuffer); collectionContentBuffer = collectionContentBuffer.Slice(4);

								{data.ItemTypeInfo.FullyQualifiedTypeName} item = (({NetworkCollectionPrefix})this).{data.Name}[index];
								{GenerateDeserialization(data.ItemDeserializationExpression, "collectionContentBuffer")}

								collectionContentBuffer = collectionContentBuffer.Slice(itemLength);

								if (iSerializable.SerializationContext?.DeserializeDirtiesProperty == true) {{
									iSerializable.SerializationContext.MarkForClean(iSerializable.Anchor);
									operationBuffer.Add(new {NetworkCollectionOperationFQN}<{data.ItemTypeInfo.FullyQualifiedTypeName}> {{
										OperationType = {NetworkCollectionOperationTypeFQN}.Update,
										Index = index,
										Value = item
									}});
								}}

								break;
							}}
						");
					}
				}
			}
		}
	}
}