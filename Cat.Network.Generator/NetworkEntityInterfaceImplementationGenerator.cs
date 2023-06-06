using System;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using static Cat.Network.Generator.Utils;

namespace Cat.Network.Generator {
	public static class NetworkEntityInterfaceImplementationGenerator {

		public static string GenerateNetworkEntitySource(NetworkEntityClassDefinition classDefinition) {

			return $@"

namespace {classDefinition.Namespace} {{

	partial class {classDefinition.Name} : {NetworkEntityInterfaceFQN} {{

{GenerateNetworkEntityInitializer(classDefinition)}
{GenerateNetworkCollectionClean(classDefinition)}
{GenerateNetworkEntitySerialize(classDefinition)}
{GenerateNetworkEntityDeserialize(classDefinition)}

	}}
}}
";
		}

		private static string GenerateNetworkEntityInitializer(NetworkEntityClassDefinition classDefinition) {
			StringBuilder stringBuilder = new StringBuilder();

			stringBuilder.AppendLine(@$"
		void {NetworkEntityInterfaceFQN}.Initialize() {{
			{NetworkPropertyInfoFQN}[] networkProperties = new {NetworkPropertyInfoFQN}[{classDefinition.NetworkProperties.Length}];
			{NetworkEntityInterfaceFQN} iEntity = this;
			iEntity.NetworkProperties = networkProperties;
");

			for (int i = 0; i < classDefinition.NetworkProperties.Length; i++) {
				NetworkPropertyData data = classDefinition.NetworkProperties[i];
				stringBuilder.AppendLine($"\t\t\tnetworkProperties[{i}] = new {NetworkPropertyInfoFQN} {{ Index = {i}, Name = \"{data.Name}\" }};");
			}

			foreach (NetworkCollectionData data in classDefinition.NetworkCollections) {
				stringBuilder.AppendLine($"\t\t\t(({NetworkCollectionPrefix})this).{data.BackingCollectionName} = new {NetworkListFQN}<{data.ItemTypeInfo.FullyQualifiedTypeName}>(this, (({NetworkCollectionPrefix})this).{data.Name});");
			}

			stringBuilder.AppendLine($"\t\t}}");

			return stringBuilder.ToString();
		}

		private static string GenerateNetworkCollectionClean(NetworkEntityClassDefinition classDefinition) {
			StringBuilder stringBuilder = new StringBuilder();

			stringBuilder.AppendLine($"\t\tvoid {NetworkEntityInterfaceFQN}.Clean() {{");

			foreach (NetworkCollectionData collection in classDefinition.NetworkCollections) {
				stringBuilder.AppendLine($"\t\t\t(({NetworkCollectionInterfaceFQN}<{collection.ItemTypeInfo.FullyQualifiedTypeName}>){collection.Name}).OperationBuffer.Clear();");
			}

			stringBuilder.AppendLine($"\t\t}}");

			return stringBuilder.ToString();
		}

		private static string GenerateNetworkEntitySerialize(NetworkEntityClassDefinition classDefinition) {
			StringBuilder stringBuilder = new StringBuilder();

			stringBuilder.AppendLine(@$"
		System.Int32 {NetworkEntityInterfaceFQN}.Serialize({SerializationOptionsFQN} serializationOptions, {SpanFQN} buffer) {{
			{SpanFQN} propertyContentBuffer = buffer.Slice(8);
			{NetworkEntityInterfaceFQN} iEntity = this;
");

			for (int i = 0; i < classDefinition.NetworkProperties.Length; i++) {
				NetworkPropertyData data = classDefinition.NetworkProperties[i];

				stringBuilder.AppendLine(@$"
			if (iEntity.NetworkProperties[{i}].LastDirtyTick >= (iEntity.SerializationContext?.Time ?? 0)) {{
				if (serializationOptions.MemberIdentifierMode == {MemberIdentifierModeFQN}.Name) {{
					 System.Int32 lengthStorage = {UnicodeFQN}.GetBytes(iEntity.NetworkProperties[{i}].Name, propertyContentBuffer.Slice(4)); {BinaryPrimitivesFQN}.WriteInt32LittleEndian(propertyContentBuffer, lengthStorage); propertyContentBuffer = propertyContentBuffer.Slice(4 + lengthStorage);
				}}
				if (serializationOptions.MemberIdentifierMode == {MemberIdentifierModeFQN}.Index) {{
					{BinaryPrimitivesFQN}.WriteInt32LittleEndian(propertyContentBuffer, {i}); propertyContentBuffer = propertyContentBuffer.Slice(4);
				}}

				{GenerateSerialization(data.Name, data.TypeInfo, "propertyContentBuffer")}

			}}");
			}

			stringBuilder.AppendLine(@$"
			System.Int32 propertyContentLength = buffer.Slice(8).Length - propertyContentBuffer.Length;
			{BinaryPrimitivesFQN}.WriteInt32LittleEndian(buffer.Slice(4), propertyContentLength);

			{SpanFQN} collectionContentBuffer = propertyContentBuffer.Slice(4);");


			foreach (NetworkCollectionData data in classDefinition.NetworkCollections) {
				stringBuilder.AppendLine(@$"

			if (serializationOptions.MemberSerializationMode == {MemberSerializationModeFQN}.Complete) {{
				{BinaryPrimitivesFQN}.WriteInt32LittleEndian(collectionContentBuffer, {data.Name}.Count + 1); collectionContentBuffer = collectionContentBuffer.Slice(4);
				collectionContentBuffer[0] = (System.Byte){NetworkCollectionOperationTypeFQN}.Clear; collectionContentBuffer = collectionContentBuffer.Slice(1);
				
				foreach (var item in {data.Name}) {{
					collectionContentBuffer[0] = (System.Byte){NetworkCollectionOperationTypeFQN}.Add; collectionContentBuffer = collectionContentBuffer.Slice(1);
					{GenerateSerialization("item", data.ItemTypeInfo, "collectionContentBuffer")}
				}}
			}} else if (serializationOptions.MemberSerializationMode == {MemberSerializationModeFQN}.Partial) {{
				var operationBuffer = (({NetworkCollectionInterfaceFQN}<{data.ItemTypeInfo.FullyQualifiedTypeName}>){data.Name}).OperationBuffer;
				{BinaryPrimitivesFQN}.WriteInt32LittleEndian(collectionContentBuffer, operationBuffer.Count); collectionContentBuffer = collectionContentBuffer.Slice(4);

				foreach ({NetworkCollectionOperationFQN}<{data.ItemTypeInfo.FullyQualifiedTypeName}> operation in operationBuffer) {{
					switch (operation.OperationType) {{
						case {NetworkCollectionOperationTypeFQN}.Add: {{
							collectionContentBuffer[0] = (System.Byte){NetworkCollectionOperationTypeFQN}.Add; collectionContentBuffer = collectionContentBuffer.Slice(1);
							{GenerateSerialization("operation.Value", data.ItemTypeInfo, "collectionContentBuffer")}
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
							{GenerateSerialization("operation.Value", data.ItemTypeInfo, "collectionContentBuffer")}
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


			stringBuilder.AppendLine(@$"
			System.Int32 collectionContentLength = propertyContentBuffer.Slice(4).Length - collectionContentBuffer.Length;
			{BinaryPrimitivesFQN}.WriteInt32LittleEndian(propertyContentBuffer, collectionContentLength);
			System.Int32 contentLength = buffer.Slice(4).Length - collectionContentBuffer.Length;
			{BinaryPrimitivesFQN}.WriteInt32LittleEndian(buffer, contentLength);
			return contentLength + 4;
		}}");

			return stringBuilder.ToString();
		}


		private static string GenerateNetworkEntityDeserialize(NetworkEntityClassDefinition classDefinition) {
			StringBuilder stringBuilder = new StringBuilder();

			stringBuilder.AppendLine($@"
		void {NetworkEntityInterfaceFQN}.Deserialize({SerializationOptionsFQN} serializationOptions, {ReadOnlySpanFQN} buffer) {{

			{NetworkEntityInterfaceFQN} iEntity = this;
			
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
					iEntity.NetworkProperties[propertyIndex].LastDirtyTick = iEntity.SerializationContext.DeserializeDirtiesProperty ? iEntity.SerializationContext?.Time ?? 0 : 0;
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
						{GenerateDeserialization("item", data.ItemTypeInfo, "collectionContentBuffer")}

						collectionContentBuffer = collectionContentBuffer.Slice(itemLength);

						if (iEntity.SerializationContext.DeserializeDirtiesProperty) {{
							iEntity.SerializationContext.MarkForClean(this);
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
						
						if (iEntity.SerializationContext.DeserializeDirtiesProperty) {{
							iEntity.SerializationContext.MarkForClean(this);
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
						{GenerateDeserialization("item", data.ItemTypeInfo, "collectionContentBuffer")}
						
						collectionContentBuffer = collectionContentBuffer.Slice(itemLength);

						if (iEntity.SerializationContext.DeserializeDirtiesProperty) {{
							iEntity.SerializationContext.MarkForClean(this);
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

						if (iEntity.SerializationContext.DeserializeDirtiesProperty) {{
							iEntity.SerializationContext.MarkForClean(this);
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
							{GenerateDeserialization(data.Name, data.TypeInfo, "indexedPropertyBuffer")}
							break;");
				}

				return casesStringBuilder.ToString();
			}


			return stringBuilder.ToString();
		}

	}
}
