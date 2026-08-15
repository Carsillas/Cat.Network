using System.Collections.Generic;
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
			SerializeByIndex(model),
			SerializeByName(model),
			DeserializeByIndex(model),
			DeserializeByName(model),
			SerializeMethods(model),
			DeserializeMethods(model),
			AccessorMethods(model),
			model.Version,
			UpgradeCases(model));

		return SyntaxFactory.ParseCompilationUnit(source).NormalizeWhitespace().ToFullString();
	}

	private static string Namespace(NetworkObjectTypeModel model) {
		return string.IsNullOrWhiteSpace(model.Namespace)
			? string.Empty
			: string.Format(NamespaceTemplate, model.Namespace);
	}

	private static string DeserializeByIndex(NetworkObjectTypeModel model) {
		IEnumerable<(int PropertyIndex, string Body)> members = model.Properties
			.Select(property => (property.PropertyIndex, $$"""
					case {{property.PropertyIndex}}:
						Deserialize{{property.Name}}(typedTarget, valueData, context);
						break;
				"""))
			.Concat(model.Collections.Select(collection => (collection.PropertyIndex, $$"""
					case {{collection.PropertyIndex}}:
						Deserialize{{collection.Name}}(typedTarget, valueData, context);
						break;
				""")))
			.OrderBy(static member => member.PropertyIndex);

		return string.Join(
			"\n",
			members.Select(static member => member.Body));
	}

	private static string DeserializeByName(NetworkObjectTypeModel model) {
		IEnumerable<(int PropertyIndex, string Body)> members = model.Properties
			.Select(property => (property.PropertyIndex, $$"""
					case "{{EscapeStringLiteral(property.Name)}}":
						Deserialize{{property.Name}}(typedTarget, valueData, context);
						break;
				"""))
			.Concat(model.Collections.Select(collection => (collection.PropertyIndex, $$"""
					case "{{EscapeStringLiteral(collection.Name)}}":
						Deserialize{{collection.Name}}(typedTarget, valueData, context);
						break;
				""")))
			.OrderBy(static member => member.PropertyIndex);

		return string.Join(
			"\n",
			members.Select(static member => member.Body));
	}

	private static string DeserializeMethods(NetworkObjectTypeModel model) {
		return string.Join(
			"\n\n",
			model.Properties.Select(property => DeserializeMethod(model, property))
				.Concat(model.Collections.Select(collection => DeserializeMethod(model, collection))));
	}

	private static string SerializeByIndex(NetworkObjectTypeModel model) {
		IEnumerable<(int PropertyIndex, string Body)> members = model.Properties
			.Select(property => (property.PropertyIndex, $$"""
					if (options.MemberSelectionMode == global::Cat.Network.MemberSelectionMode.All || current.PropertyStates[{{property.PropertyIndex}}] != global::Cat.Network.NetworkPropertyState.Unchanged) {
						writer.WriteUInt16({{(ushort)property.PropertyIndex}});
						global::System.Range {{property.Name}}LengthRange = writer.Reserve(4);
						int {{property.Name}}ValueStart = writer.WrittenCount;
						Serialize{{property.Name}}(writer, typedTarget, context, options, current, {{property.PropertyIndex}});
						writer.WriteUInt32({{property.Name}}LengthRange, (uint)(writer.WrittenCount - {{property.Name}}ValueStart));
						fieldCount++;
					}
				"""))
			.Concat(model.Collections.Select(collection => (collection.PropertyIndex, $$"""
					if (options.MemberSelectionMode == global::Cat.Network.MemberSelectionMode.All || current.PropertyStates[{{collection.PropertyIndex}}] != global::Cat.Network.NetworkPropertyState.Unchanged) {
						writer.WriteUInt16({{(ushort)collection.PropertyIndex}});
						global::System.Range {{collection.Name}}LengthRange = writer.Reserve(4);
						int {{collection.Name}}ValueStart = writer.WrittenCount;
						Serialize{{collection.Name}}(writer, typedTarget, context, options);
						writer.WriteUInt32({{collection.Name}}LengthRange, (uint)(writer.WrittenCount - {{collection.Name}}ValueStart));
						fieldCount++;
					}
				""")))
			.OrderBy(static member => member.PropertyIndex);

		return string.Join(
			"\n",
			members.Select(static member => member.Body));
	}

	private static string SerializeByName(NetworkObjectTypeModel model) {
		IEnumerable<(int PropertyIndex, string Body)> members = model.Properties
			.Select(property => (property.PropertyIndex, $$"""
					if (options.MemberSelectionMode == global::Cat.Network.MemberSelectionMode.All || current.PropertyStates[{{property.PropertyIndex}}] != global::Cat.Network.NetworkPropertyState.Unchanged) {
						writer.WriteLengthPrefixedUtf8("{{EscapeStringLiteral(property.Name)}}");
						global::System.Range {{property.Name}}LengthRange = writer.Reserve(4);
						int {{property.Name}}ValueStart = writer.WrittenCount;
						Serialize{{property.Name}}(writer, typedTarget, context, options, current, {{property.PropertyIndex}});
						writer.WriteUInt32({{property.Name}}LengthRange, (uint)(writer.WrittenCount - {{property.Name}}ValueStart));
						fieldCount++;
					}
				"""))
			.Concat(model.Collections.Select(collection => (collection.PropertyIndex, $$"""
					if (options.MemberSelectionMode == global::Cat.Network.MemberSelectionMode.All || current.PropertyStates[{{collection.PropertyIndex}}] != global::Cat.Network.NetworkPropertyState.Unchanged) {
						writer.WriteLengthPrefixedUtf8("{{EscapeStringLiteral(collection.Name)}}");
						global::System.Range {{collection.Name}}LengthRange = writer.Reserve(4);
						int {{collection.Name}}ValueStart = writer.WrittenCount;
						Serialize{{collection.Name}}(writer, typedTarget, context, options);
						writer.WriteUInt32({{collection.Name}}LengthRange, (uint)(writer.WrittenCount - {{collection.Name}}ValueStart));
						fieldCount++;
					}
				""")))
			.OrderBy(static member => member.PropertyIndex);

		return string.Join(
			"\n",
			members.Select(static member => member.Body));
	}

	private static string SerializeMethods(NetworkObjectTypeModel model) {
		return string.Join(
			"\n\n",
			model.Properties.Select(property => SerializeMethod(model, property))
				.Concat(model.Collections.Select(collection => SerializeMethod(model, collection))));
	}

	private static string AccessorMethods(NetworkObjectTypeModel model) => string.Join(
		"\n\n",
		model.Properties.Select(AccessorMethods)
			.Concat(model.Collections.Select(AccessorMethods)));

	private static string UpgradeCases(NetworkObjectTypeModel model) {
		return string.Join(
			"\n",
			model.UpgradeMethods.Select(method => $$"""
					case {{method.TargetVersion}}:
						{{model.FullyQualifiedName}}.__CatNetworkUpgradeTo{{method.TargetVersion}}(upgradeReader, upgradeWriter);
						return true;
				"""));
	}

	private static string SerializeMethod(NetworkObjectTypeModel model, NetworkPropertyModel property) {
		return $$"""
			private static void Serialize{{property.Name}}(global::Cat.Network.BufferWriter writer, {{model.FullyQualifiedName}} typedTarget, global::Cat.Network.SerializationContext context, global::Cat.Network.SerializationOptions options, global::Cat.Network.INetworkObject current, int propertyIndex) {
			{{SerializePropertyBody(property, property.PropertyIndex)}}
			}
			""";
	}

	private static string DeserializeMethod(NetworkObjectTypeModel model, NetworkPropertyModel property) {
		return $$"""
			private static void Deserialize{{property.Name}}({{model.FullyQualifiedName}} typedTarget, global::System.ReadOnlySpan<byte> valueData, global::Cat.Network.SerializationContext context) {
			{{MaybeWrapNullableValueType(property, DeserializePropertyBody(property))}}
			}
			""";
	}

	private static string SerializeMethod(NetworkObjectTypeModel model, NetworkCollectionModel collection) {
		return $$"""
			private static void Serialize{{collection.Name}}(global::Cat.Network.BufferWriter writer, {{model.FullyQualifiedName}} typedTarget, global::Cat.Network.SerializationContext context, global::Cat.Network.SerializationOptions options) {
				if (Get{{collection.Name}}(typedTarget) is global::Cat.Network.INetworkCollection collectionValue) {
					collectionValue.Serialize(writer, context, options);
				}
			}
			""";
	}

	private static string DeserializeMethod(NetworkObjectTypeModel model, NetworkCollectionModel collection) {
		return $$"""
			private static void Deserialize{{collection.Name}}({{model.FullyQualifiedName}} typedTarget, global::System.ReadOnlySpan<byte> valueData, global::Cat.Network.SerializationContext context) {
				if (Get{{collection.Name}}(typedTarget) is global::Cat.Network.INetworkCollection collectionValue) {
					collectionValue.Deserialize(valueData, context);
				}
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
					if (valueData.Length < 1) {
						return;
					}

					byte hasValue = valueData[0];
					valueData = valueData[1..];
					switch (hasValue) {
						case 0:
							if (valueData.Length != 0) {
								return;
							}

							Set{{property.Name}}(typedTarget, null!);
							break;
						case 1:
							Set{{property.Name}}(typedTarget, global::System.Text.Encoding.UTF8.GetString(valueData));
							break;
						default:
							return;
					}
					""";
			case NetworkPropertySerializationKind.Guid:
				return $$"""
					if (valueData.Length != 16) {
						return;
					}

					Set{{property.Name}}(typedTarget, new global::System.Guid(valueData));
					""";
			case NetworkPropertySerializationKind.Struct:
				return DeserializeStructBody(property);
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

	private static string SerializePropertyBody(NetworkPropertyModel property, int propertyIndex) {
		switch (property.SerializationKind) {
			case NetworkPropertySerializationKind.Boolean:
				return SerializeScalarPropertyBody(property, "value ? (byte)1 : (byte)0", 1);
			case NetworkPropertySerializationKind.Byte:
				return SerializeScalarPropertyBody(property, "(byte)value", 1);
			case NetworkPropertySerializationKind.SByte:
				return SerializeScalarPropertyBody(property, "unchecked((byte)value)", 1);
			case NetworkPropertySerializationKind.Int16:
				return SerializeBinaryPrimitivePropertyBody(property, "WriteInt16");
			case NetworkPropertySerializationKind.UInt16:
				return SerializeBinaryPrimitivePropertyBody(property, "WriteUInt16");
			case NetworkPropertySerializationKind.Int32:
				return SerializeBinaryPrimitivePropertyBody(property, "WriteInt32");
			case NetworkPropertySerializationKind.UInt32:
				return SerializeBinaryPrimitivePropertyBody(property, "WriteUInt32");
			case NetworkPropertySerializationKind.Int64:
				return SerializeBinaryPrimitivePropertyBody(property, "WriteInt64");
			case NetworkPropertySerializationKind.UInt64:
				return SerializeBinaryPrimitivePropertyBody(property, "WriteUInt64");
			case NetworkPropertySerializationKind.Single:
				return SerializeBinaryPrimitivePropertyBody(property, "WriteSingle");
			case NetworkPropertySerializationKind.Double:
				return SerializeBinaryPrimitivePropertyBody(property, "WriteDouble");
			case NetworkPropertySerializationKind.String:
				return $$"""
					global::System.String? value = Get{{property.Name}}(typedTarget);
					if (value is null) {
						writer.WriteByte(0);
						return;
					}

					writer.WriteByte(1);
					writer.WriteUtf8(value);
					return;
					""";
			case NetworkPropertySerializationKind.Guid:
				if (property.IsNullableValueType) {
					return $$"""
						{{property.TypeName}} currentValue = Get{{property.Name}}(typedTarget);
						if (!currentValue.HasValue) {
							writer.WriteByte(0);
							return;
						}

						writer.WriteByte(1);
						writer.WriteGuid(currentValue.Value);
						return;
						""";
				}

				return $$"""
					global::System.Guid value = Get{{property.Name}}(typedTarget);
					writer.WriteGuid(value);
					return;
					""";
			case NetworkPropertySerializationKind.Struct:
				return SerializeStructPropertyBody(property);
			case NetworkPropertySerializationKind.NetworkObject:
				return SerializeNetworkObjectPropertyBody(property, propertyIndex);
			default:
				return """
					return;
					""";
		}
	}

	private static string SerializeScalarPropertyBody(NetworkPropertyModel property, string writtenValueExpression, int byteLength) {
		if (property.IsNullableValueType) {
			return $$"""
				{{property.TypeName}} currentValue = Get{{property.Name}}(typedTarget);
				if (!currentValue.HasValue) {
					writer.WriteByte(0);
					return;
				}

				{{property.RuntimeTypeName}} value = currentValue.Value;
				writer.WriteByte(1);
				writer.WriteByte({{writtenValueExpression}});
				return;
				""";
		}

		return $$"""
			{{property.RuntimeTypeName}} value = Get{{property.Name}}(typedTarget);
			writer.WriteByte({{writtenValueExpression}});
			return;
			""";
	}

	private static string SerializeBinaryPrimitivePropertyBody(NetworkPropertyModel property, string writeMethod) {
		if (property.IsNullableValueType) {
			return $$"""
				{{property.TypeName}} currentValue = Get{{property.Name}}(typedTarget);
				if (!currentValue.HasValue) {
					writer.WriteByte(0);
					return;
				}

				writer.WriteByte(1);
				writer.{{writeMethod}}(currentValue.Value);
				return;
				""";
		}

		return $$"""
			writer.{{writeMethod}}(Get{{property.Name}}(typedTarget));
			return;
			""";
	}

	private static string SerializeStructPropertyBody(NetworkPropertyModel property) {
		string structAccessor = property.IsNullableValueType ? "currentValue.Value" : "currentValue";
		string fieldBodies = string.Join(
			"\n\n",
			property.StructFields.Select(field => SerializeStructFieldBody(field, structAccessor, "__" + field.Name)));

		if (property.IsNullableValueType) {
			return $$"""
				{{property.TypeName}} currentValue = Get{{property.Name}}(typedTarget);
				if (!currentValue.HasValue) {
					writer.WriteByte(0);
					return;
				}

				writer.WriteByte(1);
				{{fieldBodies}}
				return;
				""";
		}

		return $$"""
			{{property.RuntimeTypeName}} currentValue = Get{{property.Name}}(typedTarget);
			{{fieldBodies}}
			return;
			""";
	}

	private static string SerializeStructFieldBody(NetworkStructFieldModel field, string targetExpression, string fieldPath) {
		return $$"""
			{
			{{SerializeStructFieldBlock(field, targetExpression, fieldPath)}}
			}
			""";
	}

	private static string SerializeStructFieldBlock(NetworkStructFieldModel field, string targetExpression, string fieldPath) {
		if (field.IsNullableValueType) {
			if (field.SerializationKind == NetworkPropertySerializationKind.Struct) {
				string nestedAccessor = $"{fieldPath}.Value";
				string nestedBody = string.Join(
					"\n\n",
					field.StructFields.Select(nestedField => SerializeStructFieldBody(nestedField, nestedAccessor, fieldPath + "__" + nestedField.Name)));

				return $$"""
					{{field.TypeName}} {{fieldPath}} = {{targetExpression}}.{{field.Name}};
					if (!{{fieldPath}}.HasValue) {
						writer.WriteByte(0);
					} else {
						writer.WriteByte(1);
						{{nestedBody}}
					}
					""";
			}

			return $$"""
				{{field.TypeName}} {{fieldPath}} = {{targetExpression}}.{{field.Name}};
				if (!{{fieldPath}}.HasValue) {
					writer.WriteByte(0);
				} else {
					writer.WriteByte(1);
					{{SerializeNonNullableStructFieldWrite(field, fieldPath + ".Value")}}
				}
				""";
		}

		if (field.SerializationKind == NetworkPropertySerializationKind.Struct) {
			string nestedBody = string.Join(
				"\n\n",
				field.StructFields.Select(nestedField => SerializeStructFieldBody(nestedField, fieldPath, fieldPath + "__" + nestedField.Name)));

			return $$"""
				{{field.RuntimeTypeName}} {{fieldPath}} = {{targetExpression}}.{{field.Name}};
				{{nestedBody}}
				""";
		}

		return SerializeNonNullableStructFieldWrite(field, $"{targetExpression}.{field.Name}");
	}

	private static string SerializeNonNullableStructFieldWrite(NetworkStructFieldModel field, string valueExpression) {
		return field.SerializationKind switch {
			NetworkPropertySerializationKind.Boolean => $$"""
				writer.WriteByte({{valueExpression}} ? (byte)1 : (byte)0);
				""",
			NetworkPropertySerializationKind.Byte => $$"""
				writer.WriteByte({{valueExpression}});
				""",
			NetworkPropertySerializationKind.SByte => $$"""
				writer.WriteByte(unchecked((byte){{valueExpression}}));
				""",
			NetworkPropertySerializationKind.Int16 => $$"""
				writer.WriteInt16({{valueExpression}});
				""",
			NetworkPropertySerializationKind.UInt16 => $$"""
				writer.WriteUInt16({{valueExpression}});
				""",
			NetworkPropertySerializationKind.Int32 => $$"""
				writer.WriteInt32({{valueExpression}});
				""",
			NetworkPropertySerializationKind.UInt32 => $$"""
				writer.WriteUInt32({{valueExpression}});
				""",
			NetworkPropertySerializationKind.Int64 => $$"""
				writer.WriteInt64({{valueExpression}});
				""",
			NetworkPropertySerializationKind.UInt64 => $$"""
				writer.WriteUInt64({{valueExpression}});
				""",
			NetworkPropertySerializationKind.Single => $$"""
				writer.WriteSingle({{valueExpression}});
				""",
			NetworkPropertySerializationKind.Double => $$"""
				writer.WriteDouble({{valueExpression}});
				""",
			NetworkPropertySerializationKind.String => $$"""
				if ({{valueExpression}} is null) {
					writer.WriteByte(0);
				} else {
					writer.WriteByte(1);
					writer.WriteLengthPrefixedUtf8({{valueExpression}});
				}
				""",
			NetworkPropertySerializationKind.Guid => $$"""
				writer.WriteGuid({{valueExpression}});
				""",
			_ => string.Empty
		};
	}

	private static string SerializeNetworkObjectPropertyBody(NetworkPropertyModel property, int propertyIndex) {
		return $$"""
			{{property.TypeName}} currentValue = Get{{property.Name}}(typedTarget);
			if (options.MemberSelectionMode == global::Cat.Network.MemberSelectionMode.Dirty) {
				global::Cat.Network.NetworkPropertyState propertyState = current.PropertyStates[propertyIndex];
				if ((propertyState & global::Cat.Network.NetworkPropertyState.Replaced) != 0) {
					if (currentValue is null) {
						writer.WriteByte((byte)global::Cat.Network.NetworkObjectUpdateMode.Clear);
						return;
					}

					if (!context.TypeCatalogue.TryFindSerializer(currentValue.GetType(), out global::Cat.Network.INetworkObjectSerializer? nestedReplacementSerializer)) {
						throw new global::System.InvalidOperationException($"Serializer for type '{currentValue.GetType().FullName}' is not registered.");
					}

					global::System.Guid nestedReplacementTypeId = GetNetworkObjectTypeId(currentValue.GetType());
					writer.WriteByte((byte)global::Cat.Network.NetworkObjectUpdateMode.Replace);
					writer.WriteGuid(nestedReplacementTypeId);
					nestedReplacementSerializer.Serialize(writer, currentValue, context, new global::Cat.Network.SerializationOptions(global::Cat.Network.MemberSelectionMode.All, options.MemberIdentificationMode));
					return;
				}

				if ((propertyState & global::Cat.Network.NetworkPropertyState.Modified) != 0) {
					if (currentValue is null) {
						return;
					}

					if (!context.TypeCatalogue.TryFindSerializer(currentValue.GetType(), out global::Cat.Network.INetworkObjectSerializer? nestedModifiedSerializer)) {
						throw new global::System.InvalidOperationException($"Serializer for type '{currentValue.GetType().FullName}' is not registered.");
					}

					writer.WriteByte((byte)global::Cat.Network.NetworkObjectUpdateMode.Modify);
					nestedModifiedSerializer.Serialize(writer, currentValue, context, new global::Cat.Network.SerializationOptions(global::Cat.Network.MemberSelectionMode.Dirty, options.MemberIdentificationMode));
					return;
				}
			}

			if (currentValue is null) {
				writer.WriteByte((byte)global::Cat.Network.NetworkObjectUpdateMode.Clear);
				return;
			}

			if (!context.TypeCatalogue.TryFindSerializer(currentValue.GetType(), out global::Cat.Network.INetworkObjectSerializer? nestedSerializer)) {
				throw new global::System.InvalidOperationException($"Serializer for type '{currentValue.GetType().FullName}' is not registered.");
			}

			global::System.Guid nestedTypeId = GetNetworkObjectTypeId(currentValue.GetType());
			writer.WriteByte((byte)global::Cat.Network.NetworkObjectUpdateMode.Replace);
			writer.WriteGuid(nestedTypeId);
			nestedSerializer.Serialize(writer, currentValue, context, new global::Cat.Network.SerializationOptions(global::Cat.Network.MemberSelectionMode.All, options.MemberIdentificationMode));
			return;
			""";
	}

	private static string BinaryPrimitiveBody(NetworkPropertyModel property, string binaryPrimitiveMethod, int byteLength) {
		return $$"""
			if (valueData.Length != {{byteLength}}) {
				return;
			}

			Set{{property.Name}}(typedTarget, global::System.Buffers.Binary.BinaryPrimitives.{{binaryPrimitiveMethod}}(valueData));
			""";
	}

	private static string DeserializeStructBody(NetworkPropertyModel property) {
		string fieldBodies = string.Join(
			"\n\n",
			property.StructFields.Select(field => DeserializeStructFieldBody(field, "structValue", "__" + field.Name)));

		return $$"""
			{{property.RuntimeTypeName}} structValue = default;

			{{fieldBodies}}

			if (!valueData.IsEmpty) {
				return;
			}

			Set{{property.Name}}(typedTarget, structValue);
			""";
	}

	private static string DeserializeStructFieldBody(NetworkStructFieldModel field, string targetExpression, string fieldPath) {
		return $$"""
			{
			{{DeserializeStructFieldBlock(field, targetExpression, fieldPath)}}
			}
			""";
	}

	private static string DeserializeStructFieldBlock(NetworkStructFieldModel field, string targetExpression, string fieldPath) {
		if (field.IsNullableValueType) {
			if (field.SerializationKind == NetworkPropertySerializationKind.Struct) {
				string nestedBody = string.Join(
					"\n\n",
					field.StructFields.Select(nestedField => DeserializeStructFieldBody(nestedField, fieldPath, fieldPath + "__" + nestedField.Name)));

				return $$"""
					if (valueData.Length < 1) {
						return;
					}

					byte has{{field.Name}}Value = valueData[0];
					valueData = valueData[1..];
					switch (has{{field.Name}}Value) {
						case 0:
							{{targetExpression}}.{{field.Name}} = null;
							break;
						case 1:
							{{field.RuntimeTypeName}} {{fieldPath}} = default;

							{{nestedBody}}

							{{targetExpression}}.{{field.Name}} = {{fieldPath}};
							break;
						default:
							return;
					}
					""";
			}

			return $$"""
				if (valueData.Length < 1) {
					return;
				}

				byte has{{field.Name}}Value = valueData[0];
				valueData = valueData[1..];
				switch (has{{field.Name}}Value) {
					case 0:
						{{targetExpression}}.{{field.Name}} = null;
						break;
					case 1:
						{{DeserializeNonNullableStructFieldAssignment(field, targetExpression)}}
						break;
					default:
						return;
				}
				""";
		}

		if (field.SerializationKind == NetworkPropertySerializationKind.Struct) {
			string nestedBody = string.Join(
				"\n\n",
				field.StructFields.Select(nestedField => DeserializeStructFieldBody(nestedField, fieldPath, fieldPath + "__" + nestedField.Name)));

			return $$"""
				{{field.RuntimeTypeName}} {{fieldPath}} = default;

				{{nestedBody}}

				{{targetExpression}}.{{field.Name}} = {{fieldPath}};
				""";
		}

		return DeserializeNonNullableStructFieldAssignment(field, targetExpression);
	}

	private static string DeserializeNonNullableStructFieldAssignment(NetworkStructFieldModel field, string targetExpression) {
		return field.SerializationKind switch {
			NetworkPropertySerializationKind.Boolean => $$"""
				if (valueData.Length < 1) {
					return;
				}

				{{targetExpression}}.{{field.Name}} = valueData[0] != 0;
				valueData = valueData[1..];
				""",
			NetworkPropertySerializationKind.Byte => $$"""
				if (valueData.Length < 1) {
					return;
				}

				{{targetExpression}}.{{field.Name}} = valueData[0];
				valueData = valueData[1..];
				""",
			NetworkPropertySerializationKind.SByte => $$"""
				if (valueData.Length < 1) {
					return;
				}

				{{targetExpression}}.{{field.Name}} = unchecked((sbyte)valueData[0]);
				valueData = valueData[1..];
				""",
			NetworkPropertySerializationKind.Int16 => StructBinaryPrimitiveBody(field, targetExpression, "ReadInt16LittleEndian", 2),
			NetworkPropertySerializationKind.UInt16 => StructBinaryPrimitiveBody(field, targetExpression, "ReadUInt16LittleEndian", 2),
			NetworkPropertySerializationKind.Int32 => StructBinaryPrimitiveBody(field, targetExpression, "ReadInt32LittleEndian", 4),
			NetworkPropertySerializationKind.UInt32 => StructBinaryPrimitiveBody(field, targetExpression, "ReadUInt32LittleEndian", 4),
			NetworkPropertySerializationKind.Int64 => StructBinaryPrimitiveBody(field, targetExpression, "ReadInt64LittleEndian", 8),
			NetworkPropertySerializationKind.UInt64 => StructBinaryPrimitiveBody(field, targetExpression, "ReadUInt64LittleEndian", 8),
			NetworkPropertySerializationKind.Single => StructBinaryPrimitiveBody(field, targetExpression, "ReadSingleLittleEndian", 4),
			NetworkPropertySerializationKind.Double => StructBinaryPrimitiveBody(field, targetExpression, "ReadDoubleLittleEndian", 8),
			NetworkPropertySerializationKind.String => $$"""
				if (valueData.Length < 1) {
					return;
				}

				byte has{{field.Name}}Value = valueData[0];
				valueData = valueData[1..];
				switch (has{{field.Name}}Value) {
					case 0:
						{{targetExpression}}.{{field.Name}} = null!;
						break;
					case 1:
						if (valueData.Length < 4) {
							return;
						}

						uint stringByteCount = global::System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(valueData);
						valueData = valueData[4..];
						if (valueData.Length < stringByteCount) {
							return;
						}

						{{targetExpression}}.{{field.Name}} = global::System.Text.Encoding.UTF8.GetString(valueData[..(int)stringByteCount]);
						valueData = valueData[(int)stringByteCount..];
						break;
					default:
						return;
				}
				""",
			NetworkPropertySerializationKind.Guid => $$"""
				if (valueData.Length < 16) {
					return;
				}

				{{targetExpression}}.{{field.Name}} = new global::System.Guid(valueData[..16]);
				valueData = valueData[16..];
				""",
			_ => """
				return;
				"""
		};
	}

	private static string StructBinaryPrimitiveBody(NetworkStructFieldModel field, string targetExpression, string binaryPrimitiveMethod, int byteLength) {
		return $$"""
			if (valueData.Length < {{byteLength}}) {
				return;
			}

			{{targetExpression}}.{{field.Name}} = global::System.Buffers.Binary.BinaryPrimitives.{{binaryPrimitiveMethod}}(valueData);
			valueData = valueData[{{byteLength}}..];
			""";
	}

	private static string MaybeWrapNullableValueType(NetworkPropertyModel property, string body) {
		if (!property.IsNullableValueType) {
			return body;
		}

		return $$"""
			if (valueData.Length < 1) {
				return;
			}

			byte hasValue = valueData[0];
			valueData = valueData[1..];
			switch (hasValue) {
				case 0:
					if (valueData.Length != 0) {
						return;
					}

					Set{{property.Name}}(typedTarget, null);
					return;
				case 1:
					break;
				default:
					return;
			}

			{{body}}
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

	private static string AccessorMethods(NetworkCollectionModel collection) {
		return $$"""
			[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = "get_{{collection.Name}}")]
			private static extern {{collection.TypeName}} Get{{collection.Name}}({{collection.DeclaringTypeName}} target);
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
	                                      	private static ushort SchemaVersion => {10};

	                                      	public void Serialize(global::Cat.Network.BufferWriter writer, global::Cat.Network.NetworkObject target, global::Cat.Network.SerializationContext context, global::Cat.Network.SerializationOptions options) {{
	                                      		{2} typedTarget = ({2})target;
	                                      		global::Cat.Network.INetworkObject current = typedTarget;
	                                      		writer.WriteUInt16(SchemaVersion);
	                                      		writer.WriteByte((byte)options.MemberIdentificationMode);
	                                      		global::System.Range fieldCountRange = writer.Reserve(2);
	                                      		ushort fieldCount = 0;

	                                      		switch (options.MemberIdentificationMode) {{
	                                      			case global::Cat.Network.MemberIdentificationMode.Index: {{
	                                      {3}
	                                      				break;
	                                      			}}
	                                      			case global::Cat.Network.MemberIdentificationMode.Name: {{
	                                      {4}
	                                      				break;
	                                      			}}
	                                      			default:
	                                      				throw new global::System.InvalidOperationException($"Unsupported member identification mode '{{options.MemberIdentificationMode}}'.");
	                                      		}}

	                                      		writer.WriteUInt16(fieldCountRange, fieldCount);

	                                      	}}

	                                      	public void Deserialize(global::Cat.Network.NetworkObject target, global::System.ReadOnlySpan<byte> data, global::Cat.Network.SerializationContext context) {{
	                                      		{2} typedTarget = ({2})target;
	                                      		if (data.Length < 5) {{
	                                      			return;
	                                      		}}

	                                      		ushort payloadVersion = global::System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(data);
	                                      		data = data[2..];
	                                      		if (payloadVersion != SchemaVersion) {{
	                                      			if (payloadVersion > SchemaVersion) {{
	                                      				return;
	                                      			}}

	                                      			if (!TryUpgradePayload(payloadVersion, data, context, out byte[] upgradedData)) {{
	                                      				return;
	                                      			}}

	                                      			data = upgradedData;
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
	                                      {5}
	                                      						default:
	                                      							break;
	                                      					}}
	                                      					break;
	                                      				case global::Cat.Network.MemberIdentificationMode.Name:
	                                      					switch (memberName) {{
	                                      {6}
	                                      						default:
	                                      							break;
	                                      					}}
	                                      					break;
	                                      			}}
	                                      		}}
	                                      	}}

	                                      	private static bool TryUpgradePayload(ushort payloadVersion, global::System.ReadOnlySpan<byte> data, global::Cat.Network.SerializationContext context, out byte[] upgradedData) {{
	                                      		upgradedData = global::System.Array.Empty<byte>();
	                                      		while (payloadVersion < SchemaVersion) {{
	                                      			if (data.Length < 1) {{
	                                      				return false;
	                                      			}}

	                                      			global::Cat.Network.MemberIdentificationMode upgradeMemberIdentificationMode = (global::Cat.Network.MemberIdentificationMode)data[0];
	                                      			if (upgradeMemberIdentificationMode == global::Cat.Network.MemberIdentificationMode.Index) {{
	                                      				throw new global::System.InvalidOperationException("Index-mode payloads do not support NetworkObject version upgrades.");
	                                      			}}

	                                      			if (!global::Cat.Network.NetworkObjectUpgradeReader.TryCreate(data, context, out global::Cat.Network.NetworkObjectUpgradeReader? upgradeReader)) {{
	                                      				return false;
	                                      			}}

	                                      			global::Cat.Network.BufferWriter upgradeBuffer = new();
	                                      			global::Cat.Network.NetworkObjectUpgradeWriter upgradeWriter = new(upgradeBuffer, upgradeReader, context);
	                                      			ushort targetVersion = (ushort)(payloadVersion + 1);
	                                      			if (!TryApplyUpgradeStep(targetVersion, upgradeReader, upgradeWriter)) {{
	                                      				return false;
	                                      			}}

	                                      			upgradedData = upgradeWriter.Complete();
	                                      			data = upgradedData;
	                                      			payloadVersion = targetVersion;
	                                      		}}

	                                      		return payloadVersion == SchemaVersion;
	                                      	}}

	                                      	private static bool TryApplyUpgradeStep(ushort targetVersion, global::Cat.Network.NetworkObjectUpgradeReader upgradeReader, global::Cat.Network.NetworkObjectUpgradeWriter upgradeWriter) {{
	                                      		switch (targetVersion) {{
	                                      {11}
	                                      			default:
	                                      				return false;
	                                      		}}
	                                      	}}

	                                      {7}

	                                      {8}

	                                      {9}

	                                      	private static global::System.Guid GetNetworkObjectTypeId(global::System.Type type) {{
	                                      		global::Cat.Network.NetworkObjectTypeId? typeId = global::System.Attribute.GetCustomAttribute(type, typeof(global::Cat.Network.NetworkObjectTypeId), false) as global::Cat.Network.NetworkObjectTypeId;
	                                      		if (typeId is null) {{
	                                      			throw new global::System.InvalidOperationException($"Type '{{type.FullName}}' is missing '{{typeof(global::Cat.Network.NetworkObjectTypeId).FullName}}'.");
	                                      		}}

	                                      		return typeId.Id;
	                                      	}}
	                                      }}
	                                      """;

	private const string NamespaceTemplate = """

	                                         namespace {0};

	                                         """;
}
