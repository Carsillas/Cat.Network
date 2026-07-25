using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cat.Network.Generator;

internal static class NetworkObjectSerializerGenerator {
	public static void Generate(SourceProductionContext context, NetworkObjectTypeModel model) {
		context.AddSource($"{model.SerializerHintName}.g.cs", GenerateSource(model));
	}

	private static string GenerateSource(NetworkObjectTypeModel model) {
		string source = string.Format(
			SourceTemplate,
			Namespace(model),
			model.SerializerTypeName,
			model.FullyQualifiedName,
			DeserializeByIndex(model),
			DeserializeByName(model),
			DeserializeMethods(model),
			AccessorMethods(model));

		return SyntaxFactory.ParseCompilationUnit(source).NormalizeWhitespace().ToFullString();
	}

	private static string Namespace(NetworkObjectTypeModel model) {
		return string.IsNullOrWhiteSpace(model.Namespace)
			? string.Empty
			: string.Format(NamespaceTemplate, model.Namespace);
	}

	private static string DeserializeByIndex(NetworkObjectTypeModel model) {
		return string.Join(
			"\n",
			model.Properties.Select((property, index) => $$"""
					case {{index}}:
						Deserialize{{property.Name}}(typedTarget, valueData, context);
						break;
				"""));
	}

	private static string DeserializeByName(NetworkObjectTypeModel model) {
		return string.Join(
			"\n",
			model.Properties.Select(property => $$"""
					case "{{EscapeStringLiteral(property.Name)}}":
						Deserialize{{property.Name}}(typedTarget, valueData, context);
						break;
				"""));
	}

	private static string DeserializeMethods(NetworkObjectTypeModel model) {
		return string.Join(
			"\n\n",
			model.Properties.Select(property => DeserializeMethod(model, property)));
	}

	private static string AccessorMethods(NetworkObjectTypeModel model) => string.Join("\n\n", model.Properties.Select(AccessorMethods));

	private static string DeserializeMethod(NetworkObjectTypeModel model, NetworkPropertyModel property) {
		return $$"""
			private static void Deserialize{{property.Name}}({{model.FullyQualifiedName}} typedTarget, global::System.ReadOnlySpan<byte> valueData, global::Cat.Network.SerializationContext context) {
			{{DeserializePropertyBody(property)}}
			}
			""";
	}

	private static string DeserializePropertyBody(NetworkPropertyModel property) {
		switch (property.SerializationKind) {
			case NetworkPropertySerializationKind.Boolean:
				return $$"""
					if (valueData.Length != 1) {
						return;
					}

					Set{{property.Name}}(typedTarget, valueData[0] != 0);
					""";
			case NetworkPropertySerializationKind.Byte:
				return $$"""
					if (valueData.Length != 1) {
						return;
					}

					Set{{property.Name}}(typedTarget, valueData[0]);
					""";
			case NetworkPropertySerializationKind.SByte:
				return $$"""
					if (valueData.Length != 1) {
						return;
					}

					Set{{property.Name}}(typedTarget, unchecked((sbyte)valueData[0]));
					""";
			case NetworkPropertySerializationKind.Int16:
				return BinaryPrimitiveBody(property, "ReadInt16LittleEndian", 2);
			case NetworkPropertySerializationKind.UInt16:
				return BinaryPrimitiveBody(property, "ReadUInt16LittleEndian", 2);
			case NetworkPropertySerializationKind.Int32:
				return BinaryPrimitiveBody(property, "ReadInt32LittleEndian", 4);
			case NetworkPropertySerializationKind.UInt32:
				return BinaryPrimitiveBody(property, "ReadUInt32LittleEndian", 4);
			case NetworkPropertySerializationKind.Int64:
				return BinaryPrimitiveBody(property, "ReadInt64LittleEndian", 8);
			case NetworkPropertySerializationKind.UInt64:
				return BinaryPrimitiveBody(property, "ReadUInt64LittleEndian", 8);
			case NetworkPropertySerializationKind.Single:
				return BinaryPrimitiveBody(property, "ReadSingleLittleEndian", 4);
			case NetworkPropertySerializationKind.Double:
				return BinaryPrimitiveBody(property, "ReadDoubleLittleEndian", 8);
			case NetworkPropertySerializationKind.String:
				return $$"""
					Set{{property.Name}}(typedTarget, global::System.Text.Encoding.UTF8.GetString(valueData));
					""";
			case NetworkPropertySerializationKind.Guid:
				return $$"""
					if (valueData.Length != 16) {
						return;
					}

					Set{{property.Name}}(typedTarget, new global::System.Guid(valueData));
					""";
			case NetworkPropertySerializationKind.NetworkObject:
				return $$"""
					if (valueData.Length < 1) {
						return;
					}

					global::Cat.Network.NetworkObjectUpdateMode NetworkObjectUpdateMode = (global::Cat.Network.NetworkObjectUpdateMode)valueData[0];
					valueData = valueData[1..];

					switch (NetworkObjectUpdateMode) {
						case global::Cat.Network.NetworkObjectUpdateMode.Modify:
							{{property.TypeName}} currentTarget = Get{{property.Name}}(typedTarget);
							if (currentTarget is null) {
								return;
							}
							if (!context.TypeCatalogue.TryFindSerializer(currentTarget.GetType(), out global::Cat.Network.INetworkObjectSerializer? nestedSerializer)) {
								return;
							}

							nestedSerializer.Deserialize(currentTarget, valueData, context);
							break;
						case global::Cat.Network.NetworkObjectUpdateMode.Replace:
							if (valueData.Length < 16) {
								return;
							}

							global::System.Guid replacementTypeId = new global::System.Guid(valueData[..16]);
							valueData = valueData[16..];
							if (!context.TypeCatalogue.TryFindType(replacementTypeId, out global::System.Type? replacementType)) {
								return;
							}
							if (!typeof({{property.RuntimeTypeName}}).IsAssignableFrom(replacementType)) {
								return;
							}
							if (!context.TypeCatalogue.TryFindSerializer(replacementType, out global::Cat.Network.INetworkObjectSerializer? replacementSerializer)) {
								return;
							}
							if (global::System.Activator.CreateInstance(replacementType) is not {{property.RuntimeTypeName}} replacementTarget) {
								return;
							}

							Set{{property.Name}}(typedTarget, replacementTarget);
							replacementSerializer.Deserialize(replacementTarget, valueData, context);
							break;
						case global::Cat.Network.NetworkObjectUpdateMode.Clear:
							Set{{property.Name}}(typedTarget, null!);
							break;
						default:
							return;
					}
					""";
			default:
				return """
					return;
					""";
		}
	}

	private static string BinaryPrimitiveBody(NetworkPropertyModel property, string binaryPrimitiveMethod, int byteLength) {
		return $$"""
			if (valueData.Length != {{byteLength}}) {
				return;
			}

			Set{{property.Name}}(typedTarget, global::System.Buffers.Binary.BinaryPrimitives.{{binaryPrimitiveMethod}}(valueData));
			""";
	}

	private static string AccessorMethods(NetworkPropertyModel property) {
		return $$"""
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "get_{{property.Name}}")]
			private static extern {{property.TypeName}} Get{{property.Name}}({{property.DeclaringTypeName}} target);

			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "set_{{property.Name}}")]
			private static extern void Set{{property.Name}}({{property.DeclaringTypeName}} target, {{property.TypeName}} value);
			""";
	}

	private static string EscapeStringLiteral(string value) {
		return value
			.Replace("\\", "\\\\")
			.Replace("\"", "\\\"");
	}

	private const string SourceTemplate = """
	                                      // <auto-generated/>
	                                      #nullable enable
	                                      {0}
	                                      internal sealed class {1} : global::Cat.Network.INetworkObjectSerializer
	                                      {{
	                                      	public void Serialize(global::Cat.Network.NetworkObject target) {{
	                                      		{2} typedTarget = ({2})target;
	                                      	}}

	                                      	public void Deserialize(global::Cat.Network.NetworkObject target, global::System.ReadOnlySpan<byte> data, global::Cat.Network.SerializationContext context) {{
	                                      		{2} typedTarget = ({2})target;
	                                      		if (data.Length < 3) {{
	                                      			return;
	                                      		}}

	                                      		global::Cat.Network.MemberIdentificationMode memberIdentificationMode = (global::Cat.Network.MemberIdentificationMode)data[0];
	                                      		data = data[1..];
	                                      		ushort fieldCount = global::System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(data);
	                                      		data = data[2..];

	                                      		for (int fieldIndex = 0; fieldIndex < fieldCount; fieldIndex++) {{
	                                      			ushort memberIndex = 0;
	                                      			string? memberName = null;
	                                      			switch (memberIdentificationMode) {{
	                                      				case global::Cat.Network.MemberIdentificationMode.Index:
	                                      					if (data.Length < 2) {{
	                                      						return;
	                                      					}}

	                                      					memberIndex = global::System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(data);
	                                      					data = data[2..];
	                                      					break;
	                                      				case global::Cat.Network.MemberIdentificationMode.Name:
	                                      					if (data.Length < 4) {{
	                                      						return;
	                                      					}}

	                                      					uint nameByteCount = global::System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(data);
	                                      					data = data[4..];
	                                      					if (data.Length < nameByteCount) {{
	                                      						return;
	                                      					}}

	                                      					memberName = global::System.Text.Encoding.UTF8.GetString(data[..(int)nameByteCount]);
	                                      					data = data[(int)nameByteCount..];
	                                      					break;
	                                      				default:
	                                      					return;
	                                      			}}

	                                      			if (data.Length < 4) {{
	                                      				return;
	                                      			}}

	                                      			uint valueByteCount = global::System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(data);
	                                      			data = data[4..];
	                                      			if (data.Length < valueByteCount) {{
	                                      				return;
	                                      			}}

	                                      			global::System.ReadOnlySpan<byte> valueData = data[..(int)valueByteCount];
	                                      			data = data[(int)valueByteCount..];

	                                      			switch (memberIdentificationMode) {{
	                                      				case global::Cat.Network.MemberIdentificationMode.Index:
	                                      					switch (memberIndex) {{
	                                      {3}
	                                      						default:
	                                      							break;
	                                      					}}
	                                      					break;
	                                      				case global::Cat.Network.MemberIdentificationMode.Name:
	                                      					switch (memberName) {{
	                                      {4}
	                                      						default:
	                                      							break;
	                                      					}}
	                                      					break;
	                                      			}}
	                                      		}}
	                                      	}}

	                                      {5}

	                                      {6}
	                                      }}
	                                      """;

	private const string NamespaceTemplate = """

	                                         namespace {0};

	                                         """;
}
