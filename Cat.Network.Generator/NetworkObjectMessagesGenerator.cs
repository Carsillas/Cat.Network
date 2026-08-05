using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cat.Network.Generator;

internal static class NetworkObjectMessagesGenerator {
	public static void Generate(SourceProductionContext context, NetworkObjectTypeModel model) {
		if (!ShouldGenerate(model)) {
			return;
		}

		context.AddSource($"{model.HintName}_Messages.g.cs", GenerateSource(model));
	}

	private static bool ShouldGenerate(NetworkObjectTypeModel model) {
		return model.IsNetworkEntity &&
		       (model.DeclaredRpcs.Any() ||
		        model.DeclaredBroadcasts.Any());
	}

	private static string GenerateSource(NetworkObjectTypeModel model) {
		string source = string.Format(
			SourceTemplate,
			Namespace(model),
			model.TypeName,
			ClassInterfaces(model),
			NetworkMessages(model));

		return SyntaxFactory.ParseCompilationUnit(source).NormalizeWhitespace().ToFullString();
	}

	private static string Namespace(NetworkObjectTypeModel model) {
		return string.IsNullOrWhiteSpace(model.Namespace)
			? string.Empty
			: string.Format(NamespaceTemplate, model.Namespace);
	}

	private static string ClassInterfaces(NetworkObjectTypeModel model) {
		List<string> interfaces = [];
		if (model.DeclaredRpcs.Any() || model.DeclaredBroadcasts.Any()) {
			interfaces.Add("global::Cat.Network.INetworkRpcTarget");
		}
		if (model.DeclaredRpcs.Any()) {
			interfaces.Add($"{model.FullyQualifiedName}.RPC");
		}
		if (model.DeclaredBroadcasts.Any()) {
			interfaces.Add($"{model.FullyQualifiedName}.Broadcast");
		}

		return string.Join(", ", interfaces);
	}

	private static string NetworkMessages(NetworkObjectTypeModel model) {
		return string.Join(
			"\n\n",
			new[] {
				MessageInterface(model, NetworkMessageKind.Rpc),
				MessageInterface(model, NetworkMessageKind.Broadcast),
				MessageMembers(model.DeclaredRpcs, "RPC"),
				MessageMembers(model.DeclaredBroadcasts, "Broadcast"),
				MessageDispatch(model),
				ParameterHelpers(model)
			}.Where(static part => !string.IsNullOrWhiteSpace(part)));
	}

	private static string MessageInterface(NetworkObjectTypeModel model, NetworkMessageKind kind) {
		ImmutableArray<NetworkMessageMethodModel> declaredMessages = kind == NetworkMessageKind.Rpc
			? model.DeclaredRpcs
			: model.DeclaredBroadcasts;
		if (declaredMessages.IsEmpty) {
			return string.Empty;
		}

		bool hasBaseMessages = (kind == NetworkMessageKind.Rpc ? model.Rpcs : model.Broadcasts)
			.Any(message => message.DeclaringTypeName != model.FullyQualifiedName);
		string interfaceName = kind == NetworkMessageKind.Rpc ? "RPC" : "Broadcast";
		string inheritance = hasBaseMessages ? $" : {model.BaseTypeName}.{interfaceName}" : string.Empty;
		string newModifier = hasBaseMessages ? "new " : string.Empty;
		string members = string.Join(
			"\n\n",
			declaredMessages.Select(MessageInterfaceMember));

		return $$"""
			public {{newModifier}}partial interface {{interfaceName}}{{inheritance}}
			{
				{{members}}
			}
			""";
	}

	private static string MessageInterfaceMember(NetworkMessageMethodModel message) {
		string parameters = ReceiveParameters(message);
		if (message.ReceiveMode == NetworkMessageReceiveModeModel.Explicit) {
			return $"void {message.Name}({parameters});";
		}

		return $$"""
			event {{message.Name}}RpcHandler? {{message.Name}}Received;
			void Raise{{message.Name}}({{parameters}});
			""";
	}

	private static string MessageMembers(ImmutableArray<NetworkMessageMethodModel> messages, string interfaceName) {
		return string.Join(
			"\n\n",
			messages.Select(message => MessageMember(message, interfaceName)));
	}

	private static string MessageMember(NetworkMessageMethodModel message, string interfaceName) {
		string eventMembers = message.ReceiveMode == NetworkMessageReceiveModeModel.Event
			? $$"""
				public delegate void {{message.Name}}RpcHandler({{ReceiveParameters(message)}});

				public event {{message.Name}}RpcHandler? {{message.Name}}Received;

				void {{message.DeclaringTypeName}}.{{interfaceName}}.Raise{{message.Name}}({{ReceiveParameters(message)}})
				{
					{{message.Name}}Received?.Invoke({{ReceiveArgumentNames(message)}});
				}
				"""
			: string.Empty;

		return string.Join(
			"\n\n",
			new[] {
				eventMembers,
				MessageMethodImplementation(message)
			}.Where(static part => !string.IsNullOrWhiteSpace(part)));
	}

	private static string MessageMethodImplementation(NetworkMessageMethodModel message) {
		string parameterDeclarations = string.Join(", ", message.Parameters.Select(static parameter => $"{parameter.TypeName} {parameter.Name}"));
		string writerBody = WriteParameters(message);
		string localInvocation = message.ReceiveMode == NetworkMessageReceiveModeModel.Event
			? $"(({message.DeclaringTypeName}.{(message.Kind == NetworkMessageKind.Rpc ? "RPC" : "Broadcast")})this).Raise{message.Name}(client, instigator{ReceiveForwardedArguments(message)});"
			: $"(({message.DeclaringTypeName}.{(message.Kind == NetworkMessageKind.Rpc ? "RPC" : "Broadcast")})this).{message.Name}(client, instigator{ReceiveForwardedArguments(message)});";

		if (message.Kind == NetworkMessageKind.Broadcast) {
			return $$"""
				public partial void {{message.Name}}({{parameterDeclarations}})
				{
					if (Peer is not global::Cat.Network.RelayClient client) {
						throw new global::System.InvalidOperationException("Broadcasts can only be invoked by a connected relay client.");
					}
					if (!IsOwner) {
						throw new global::System.InvalidOperationException("Broadcasts can only be invoked by the entity owner.");
					}
					global::Cat.Network.NetworkProfile instigator = client.Profile ?? throw new global::System.InvalidOperationException("Cannot invoke a broadcast before the local profile has been assigned.");
					{{localInvocation}}
					global::Cat.Network.BufferWriter writer = client.RentBroadcastMessageWriter(this, {{message.Id}}UL, out global::Cat.Network.SerializationContext context);
					{{writerBody}}
					client.QueueRentedMessageWriter(writer);
				}
				""";
		}

		return $$"""
			public partial void {{message.Name}}({{parameterDeclarations}})
			{
				if (Peer is not global::Cat.Network.RelayClient client) {
					throw new global::System.InvalidOperationException("RPCs can only be invoked by a connected relay client.");
				}
				if (IsOwner) {
					global::Cat.Network.NetworkProfile instigator = client.Profile ?? throw new global::System.InvalidOperationException("Cannot invoke an RPC before the local profile has been assigned.");
					{{localInvocation}}
					return;
				}
				global::Cat.Network.BufferWriter writer = client.RentRpcMessageWriter(this, {{message.Id}}UL, out global::Cat.Network.SerializationContext context);
				{{writerBody}}
				client.QueueRentedMessageWriter(writer);
			}
			""";
	}

	private static string MessageDispatch(NetworkObjectTypeModel model) {
		if (model.Rpcs.IsEmpty && model.Broadcasts.IsEmpty) {
			return string.Empty;
		}

		return $$"""
			bool global::Cat.Network.INetworkRpcTarget.TryInvokeRpc(global::Cat.Network.RelayClient client, global::Cat.Network.NetworkProfile instigator, ulong rpcId, global::System.ReadOnlySpan<byte> data, global::Cat.Network.SerializationContext context)
			{
				switch (rpcId) {
					{{MessageDispatchCases(model.Rpcs, "RPC")}}
					default:
						return false;
				}
			}

			bool global::Cat.Network.INetworkRpcTarget.TryInvokeBroadcast(global::Cat.Network.RelayClient client, global::Cat.Network.NetworkProfile instigator, ulong rpcId, global::System.ReadOnlySpan<byte> data, global::Cat.Network.SerializationContext context)
			{
				switch (rpcId) {
					{{MessageDispatchCases(model.Broadcasts, "Broadcast")}}
					default:
						return false;
				}
			}
			""";
	}

	private static string MessageDispatchCases(ImmutableArray<NetworkMessageMethodModel> messages, string interfaceName) {
		return string.Join(
			"\n",
			messages.Select(message => MessageDispatchCase(message, interfaceName)));
	}

	private static string MessageDispatchCase(NetworkMessageMethodModel message, string interfaceName) {
		string readParameters = ReadParameters(message);
		string invocation = message.ReceiveMode == NetworkMessageReceiveModeModel.Event
			? $"(({message.DeclaringTypeName}.{interfaceName})this).Raise{message.Name}(client, instigator{ReceiveForwardedArguments(message)});"
			: $"(({message.DeclaringTypeName}.{interfaceName})this).{message.Name}(client, instigator{ReceiveForwardedArguments(message)});";

		return $$"""
			case {{message.Id}}UL: {
				{{readParameters}}
				if (!data.IsEmpty) {
					return false;
				}
				{{invocation}}
				return true;
			}
			""";
	}

	private static string WriteParameters(NetworkMessageMethodModel message) {
		return string.Join(
			"\n",
			message.Parameters.Select((parameter, index) => WriteParameter(parameter, index)));
	}

	private static string WriteParameter(NetworkMessageParameterModel parameter, int index) {
		string lengthRange = $"__catNetworkParameter{index}LengthRange";
		string valueStart = $"__catNetworkParameter{index}ValueStart";

		return $$"""
			{
				global::System.Range {{lengthRange}} = writer.Reserve(4);
				int {{valueStart}} = writer.WrittenCount;
				{{SerializeParameterValue(parameter, parameter.Name)}}
				writer.WriteInt32({{lengthRange}}, writer.WrittenCount - {{valueStart}});
			}
			""";
	}

	private static string ReadParameters(NetworkMessageMethodModel message) {
		return string.Join(
			"\n",
			message.Parameters.Select((parameter, index) => ReadParameter(parameter, index)));
	}

	private static string ReadParameter(NetworkMessageParameterModel parameter, int index) {
		string byteCount = $"__catNetworkParameter{index}ByteCount";
		string valueData = $"__catNetworkParameter{index}ValueData";

		return $$"""
			{{parameter.TypeName}} {{parameter.Name}};
			{
				if (data.Length < 4) {
					return false;
				}
				int {{byteCount}} = global::System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(data);
				data = data[4..];
				if ({{byteCount}} < 0 || data.Length < {{byteCount}}) {
					return false;
				}
				global::System.ReadOnlySpan<byte> {{valueData}} = data[..{{byteCount}}];
				{{DeserializeParameterValue(parameter, valueData, parameter.Name)}}
				if (!{{valueData}}.IsEmpty) {
					return false;
				}
				data = data[{{byteCount}}..];
			}
			""";
	}

	private static string SerializeParameterValue(NetworkMessageParameterModel parameter, string valueExpression) {
		if (parameter.IsNullableValueType) {
			string currentValue = $"__catNetwork{parameter.Name}Value";
			return $$"""
				{{parameter.TypeName}} {{currentValue}} = {{valueExpression}};
				if (!{{currentValue}}.HasValue) {
					writer.WriteByte(0);
				} else {
					writer.WriteByte(1);
					{{SerializeNonNullableValue(parameter.SerializationKind, parameter.RuntimeTypeName, parameter.StructFields, currentValue + ".Value", stringLengthPrefixed: false)}}
				}
				""";
		}

		return SerializeNonNullableValue(parameter.SerializationKind, parameter.RuntimeTypeName, parameter.StructFields, valueExpression, stringLengthPrefixed: false);
	}

	private static string SerializeNonNullableValue(NetworkPropertySerializationKind kind, string runtimeTypeName, ImmutableArray<NetworkStructFieldModel> structFields, string valueExpression, bool stringLengthPrefixed) {
		switch (kind) {
			case NetworkPropertySerializationKind.Boolean:
				return $"writer.WriteByte({valueExpression} ? (byte)1 : (byte)0);";
			case NetworkPropertySerializationKind.Byte:
				return $"writer.WriteByte({valueExpression});";
			case NetworkPropertySerializationKind.SByte:
				return $"writer.WriteByte(unchecked((byte){valueExpression}));";
			case NetworkPropertySerializationKind.Int16:
				return $"writer.WriteInt16({valueExpression});";
			case NetworkPropertySerializationKind.UInt16:
				return $"writer.WriteUInt16({valueExpression});";
			case NetworkPropertySerializationKind.Int32:
				return $"writer.WriteInt32({valueExpression});";
			case NetworkPropertySerializationKind.UInt32:
				return $"writer.WriteUInt32({valueExpression});";
			case NetworkPropertySerializationKind.Int64:
				return $"writer.WriteInt64({valueExpression});";
			case NetworkPropertySerializationKind.UInt64:
				return $"writer.WriteUInt64({valueExpression});";
			case NetworkPropertySerializationKind.Single:
				return $"writer.WriteSingle({valueExpression});";
			case NetworkPropertySerializationKind.Double:
				return $"writer.WriteDouble({valueExpression});";
			case NetworkPropertySerializationKind.String:
				return stringLengthPrefixed
					? $$"""
						writer.WriteLengthPrefixedUtf8({{valueExpression}} ?? throw new global::System.InvalidOperationException("String network message parameters cannot be null."));
						"""
					: $"writer.WriteUtf8({valueExpression} ?? throw new global::System.InvalidOperationException(\"String network message parameters cannot be null.\"));";
			case NetworkPropertySerializationKind.Guid:
				return $"writer.WriteGuid({valueExpression});";
			case NetworkPropertySerializationKind.Struct:
				return SerializeStructValue(runtimeTypeName, structFields, valueExpression);
			case NetworkPropertySerializationKind.NetworkObject:
				return SerializeNetworkObjectValue(valueExpression);
			default:
				return "throw new global::System.InvalidOperationException(\"Network message parameter type is not supported.\");";
		}
	}

	private static string SerializeStructValue(string runtimeTypeName, ImmutableArray<NetworkStructFieldModel> structFields, string valueExpression) {
		string currentValue = "__catNetworkStructValue";
		string fieldBodies = string.Join(
			"\n\n",
			structFields.Select(field => SerializeStructFieldBody(field, currentValue, "__catNetworkStructField" + field.Name)));

		return $$"""
			{{runtimeTypeName}} {{currentValue}} = {{valueExpression}};
			{{fieldBodies}}
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
					{{SerializeNonNullableValue(field.SerializationKind, field.RuntimeTypeName, field.StructFields, fieldPath + ".Value", stringLengthPrefixed: true)}}
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

		return SerializeNonNullableValue(field.SerializationKind, field.RuntimeTypeName, field.StructFields, $"{targetExpression}.{field.Name}", stringLengthPrefixed: true);
	}

	private static string SerializeNetworkObjectValue(string valueExpression) {
		return $$"""
			if ({{valueExpression}} is null) {
				writer.WriteByte((byte)global::Cat.Network.NetworkObjectUpdateMode.Clear);
			} else {
				global::Cat.Network.NetworkObject networkObject = {{valueExpression}};
				if (!context.TypeCatalogue.TryFindSerializer(networkObject.GetType(), out global::Cat.Network.INetworkObjectSerializer? serializer)) {
					throw new global::System.InvalidOperationException($"Serializer for type '{networkObject.GetType().FullName}' is not registered.");
				}

				writer.WriteByte((byte)global::Cat.Network.NetworkObjectUpdateMode.Replace);
				writer.WriteGuid(GetNetworkObjectTypeId(networkObject.GetType()));
				serializer.Serialize(writer, networkObject, context, new global::Cat.Network.SerializationOptions(global::Cat.Network.MemberSelectionMode.All, global::Cat.Network.MemberIdentificationMode.Name));
			}
			""";
	}

	private static string DeserializeParameterValue(NetworkMessageParameterModel parameter, string valueData, string targetName) {
		if (parameter.IsNullableValueType) {
			string localValue = $"__catNetwork{parameter.Name}Value";
			return $$"""
				if ({{valueData}}.Length < 1) {
					return false;
				}
				byte __catNetworkHasValue = {{valueData}}[0];
				{{valueData}} = {{valueData}}[1..];
				switch (__catNetworkHasValue) {
					case 0:
						{{targetName}} = null;
						break;
					case 1:
						{{parameter.RuntimeTypeName}} {{localValue}};
						{{DeserializeNonNullableValue(parameter.SerializationKind, parameter.RuntimeTypeName, parameter.StructFields, valueData, localValue, stringLengthPrefixed: false)}}
						{{targetName}} = {{localValue}};
						break;
					default:
						return false;
				}
				""";
		}

		return DeserializeNonNullableValue(parameter.SerializationKind, parameter.RuntimeTypeName, parameter.StructFields, valueData, targetName, stringLengthPrefixed: false);
	}

	private static string DeserializeNonNullableValue(NetworkPropertySerializationKind kind, string runtimeTypeName, ImmutableArray<NetworkStructFieldModel> structFields, string valueData, string targetName, bool stringLengthPrefixed) {
		switch (kind) {
			case NetworkPropertySerializationKind.Boolean:
				return $$"""
					if ({{valueData}}.Length < 1) {
						return false;
					}
					{{targetName}} = {{valueData}}[0] != 0;
					{{valueData}} = {{valueData}}[1..];
					""";
			case NetworkPropertySerializationKind.Byte:
				return $$"""
					if ({{valueData}}.Length < 1) {
						return false;
					}
					{{targetName}} = {{valueData}}[0];
					{{valueData}} = {{valueData}}[1..];
					""";
			case NetworkPropertySerializationKind.SByte:
				return $$"""
					if ({{valueData}}.Length < 1) {
						return false;
					}
					{{targetName}} = unchecked((sbyte){{valueData}}[0]);
					{{valueData}} = {{valueData}}[1..];
					""";
			case NetworkPropertySerializationKind.Int16:
				return DeserializeBinaryPrimitive(valueData, targetName, "ReadInt16LittleEndian", 2);
			case NetworkPropertySerializationKind.UInt16:
				return DeserializeBinaryPrimitive(valueData, targetName, "ReadUInt16LittleEndian", 2);
			case NetworkPropertySerializationKind.Int32:
				return DeserializeBinaryPrimitive(valueData, targetName, "ReadInt32LittleEndian", 4);
			case NetworkPropertySerializationKind.UInt32:
				return DeserializeBinaryPrimitive(valueData, targetName, "ReadUInt32LittleEndian", 4);
			case NetworkPropertySerializationKind.Int64:
				return DeserializeBinaryPrimitive(valueData, targetName, "ReadInt64LittleEndian", 8);
			case NetworkPropertySerializationKind.UInt64:
				return DeserializeBinaryPrimitive(valueData, targetName, "ReadUInt64LittleEndian", 8);
			case NetworkPropertySerializationKind.Single:
				return DeserializeBinaryPrimitive(valueData, targetName, "ReadSingleLittleEndian", 4);
			case NetworkPropertySerializationKind.Double:
				return DeserializeBinaryPrimitive(valueData, targetName, "ReadDoubleLittleEndian", 8);
			case NetworkPropertySerializationKind.String:
				return stringLengthPrefixed
					? $$"""
						if ({{valueData}}.Length < 4) {
							return false;
						}
						uint __catNetworkStringByteCount = global::System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian({{valueData}});
						{{valueData}} = {{valueData}}[4..];
						if ({{valueData}}.Length < __catNetworkStringByteCount) {
							return false;
						}
						{{targetName}} = global::System.Text.Encoding.UTF8.GetString({{valueData}}[..(int)__catNetworkStringByteCount]);
						{{valueData}} = {{valueData}}[(int)__catNetworkStringByteCount..];
						"""
					: $$"""
						{{targetName}} = global::System.Text.Encoding.UTF8.GetString({{valueData}});
						{{valueData}} = global::System.ReadOnlySpan<byte>.Empty;
						""";
			case NetworkPropertySerializationKind.Guid:
				return $$"""
					if ({{valueData}}.Length < 16) {
						return false;
					}
					{{targetName}} = new global::System.Guid({{valueData}}[..16]);
					{{valueData}} = {{valueData}}[16..];
					""";
			case NetworkPropertySerializationKind.Struct:
				return DeserializeStructValue(runtimeTypeName, structFields, valueData, targetName);
			case NetworkPropertySerializationKind.NetworkObject:
				return DeserializeNetworkObjectValue(runtimeTypeName, valueData, targetName);
			default:
				return "return false;";
		}
	}

	private static string DeserializeBinaryPrimitive(string valueData, string targetName, string methodName, int byteLength) {
		return $$"""
			if ({{valueData}}.Length < {{byteLength}}) {
				return false;
			}
			{{targetName}} = global::System.Buffers.Binary.BinaryPrimitives.{{methodName}}({{valueData}});
			{{valueData}} = {{valueData}}[{{byteLength}}..];
			""";
	}

	private static string DeserializeStructValue(string runtimeTypeName, ImmutableArray<NetworkStructFieldModel> structFields, string valueData, string targetName) {
		string fieldBodies = string.Join(
			"\n\n",
			structFields.Select(field => DeserializeStructFieldBody(field, valueData, targetName, "__catNetworkStructField" + field.Name)));

		return $$"""
			{{targetName}} = default;
			{{fieldBodies}}
			""";
	}

	private static string DeserializeStructFieldBody(NetworkStructFieldModel field, string valueData, string targetExpression, string fieldPath) {
		return $$"""
			{
			{{DeserializeStructFieldBlock(field, valueData, targetExpression, fieldPath)}}
			}
			""";
	}

	private static string DeserializeStructFieldBlock(NetworkStructFieldModel field, string valueData, string targetExpression, string fieldPath) {
		if (field.IsNullableValueType) {
			if (field.SerializationKind == NetworkPropertySerializationKind.Struct) {
				string nestedBody = string.Join(
					"\n\n",
					field.StructFields.Select(nestedField => DeserializeStructFieldBody(nestedField, valueData, fieldPath, fieldPath + "__" + nestedField.Name)));

				return $$"""
					if ({{valueData}}.Length < 1) {
						return false;
					}
					byte has{{field.Name}}Value = {{valueData}}[0];
					{{valueData}} = {{valueData}}[1..];
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
							return false;
					}
					""";
			}

			return $$"""
				if ({{valueData}}.Length < 1) {
					return false;
				}
				byte has{{field.Name}}Value = {{valueData}}[0];
				{{valueData}} = {{valueData}}[1..];
				switch (has{{field.Name}}Value) {
					case 0:
						{{targetExpression}}.{{field.Name}} = null;
						break;
					case 1:
						{{field.RuntimeTypeName}} {{fieldPath}};
						{{DeserializeNonNullableValue(field.SerializationKind, field.RuntimeTypeName, field.StructFields, valueData, fieldPath, stringLengthPrefixed: true)}}
						{{targetExpression}}.{{field.Name}} = {{fieldPath}};
						break;
					default:
						return false;
				}
				""";
		}

		if (field.SerializationKind == NetworkPropertySerializationKind.Struct) {
			string nestedBody = string.Join(
				"\n\n",
				field.StructFields.Select(nestedField => DeserializeStructFieldBody(nestedField, valueData, fieldPath, fieldPath + "__" + nestedField.Name)));

			return $$"""
				{{field.RuntimeTypeName}} {{fieldPath}} = default;
				{{nestedBody}}
				{{targetExpression}}.{{field.Name}} = {{fieldPath}};
				""";
		}

		return DeserializeNonNullableValue(field.SerializationKind, field.RuntimeTypeName, field.StructFields, valueData, $"{targetExpression}.{field.Name}", stringLengthPrefixed: true);
	}

	private static string DeserializeNetworkObjectValue(string runtimeTypeName, string valueData, string targetName) {
		return $$"""
			if ({{valueData}}.Length < 1) {
				return false;
			}
			global::Cat.Network.NetworkObjectUpdateMode __catNetworkObjectUpdateMode = (global::Cat.Network.NetworkObjectUpdateMode){{valueData}}[0];
			{{valueData}} = {{valueData}}[1..];
			switch (__catNetworkObjectUpdateMode) {
				case global::Cat.Network.NetworkObjectUpdateMode.Clear:
					{{targetName}} = null!;
					break;
				case global::Cat.Network.NetworkObjectUpdateMode.Replace:
					if ({{valueData}}.Length < 16) {
						return false;
					}
					global::System.Guid __catNetworkReplacementTypeId = new global::System.Guid({{valueData}}[..16]);
					{{valueData}} = {{valueData}}[16..];
					if (!context.TypeCatalogue.TryFindType(__catNetworkReplacementTypeId, out global::System.Type? __catNetworkReplacementType)) {
						return false;
					}
					if (!typeof({{runtimeTypeName}}).IsAssignableFrom(__catNetworkReplacementType)) {
						return false;
					}
					if (!context.TypeCatalogue.TryFindSerializer(__catNetworkReplacementType, out global::Cat.Network.INetworkObjectSerializer? __catNetworkReplacementSerializer)) {
						return false;
					}
					if (global::System.Activator.CreateInstance(__catNetworkReplacementType) is not {{runtimeTypeName}} __catNetworkReplacementTarget) {
						return false;
					}
					__catNetworkReplacementSerializer.Deserialize(__catNetworkReplacementTarget, {{valueData}}, context);
					{{targetName}} = __catNetworkReplacementTarget;
					{{valueData}} = global::System.ReadOnlySpan<byte>.Empty;
					break;
				default:
					return false;
			}
			""";
	}

	private static string ParameterHelpers(NetworkObjectTypeModel model) {
		if (model.DeclaredRpcs.IsEmpty &&
		    model.DeclaredBroadcasts.IsEmpty &&
		    (model.IsAbstract || (model.Rpcs.IsEmpty && model.Broadcasts.IsEmpty))) {
			return string.Empty;
		}

		return """
			private static global::System.Guid GetNetworkObjectTypeId(global::System.Type type)
			{
				global::Cat.Network.NetworkObjectTypeId? typeId = global::System.Attribute.GetCustomAttribute(type, typeof(global::Cat.Network.NetworkObjectTypeId), false) as global::Cat.Network.NetworkObjectTypeId;
				if (typeId is null) {
					throw new global::System.InvalidOperationException($"Type '{type.FullName}' is missing '{typeof(global::Cat.Network.NetworkObjectTypeId).FullName}'.");
				}

				return typeId.Id;
			}
			""";
	}

	private static string ReceiveParameters(NetworkMessageMethodModel message) {
		IEnumerable<string> parameters = new[] {
				"global::Cat.Network.RelayClient client",
				"global::Cat.Network.NetworkProfile instigator"
			}
			.Concat(message.Parameters.Select(static parameter => $"{parameter.TypeName} {parameter.Name}"));
		return string.Join(", ", parameters);
	}

	private static string ReceiveArgumentNames(NetworkMessageMethodModel message) {
		return string.Join(", ", new[] { "client", "instigator" }.Concat(message.Parameters.Select(static parameter => parameter.Name)));
	}

	private static string ReceiveForwardedArguments(NetworkMessageMethodModel message) {
		if (message.Parameters.IsEmpty) {
			return string.Empty;
		}

		return ", " + string.Join(", ", message.Parameters.Select(static parameter => parameter.Name));
	}

	private const string SourceTemplate = """
	                                      // <auto-generated/>
	                                      #nullable enable
	                                      {0}
	                                      partial class {1} : {2}
	                                      {{
	                                      {3}
	                                      }}
	                                      """;

	private const string NamespaceTemplate = """

	                                         namespace {0};

	                                         """;
}
