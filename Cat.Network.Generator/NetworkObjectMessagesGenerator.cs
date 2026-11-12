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
			return $"void {message.Identifier}({parameters});";
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
					this.{{message.Name}}Received?.Invoke({{ReceiveArgumentNames(message)}});
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
		MessageScope names = new(message.Parameters.Select(static parameter => parameter.Name), message.DeclaringTypeName);
		string parameterDeclarations = string.Join(", ", message.Parameters.Select(static parameter => $"{parameter.TypeName} {parameter.Identifier}"));
		string writerBody = new ParameterEmitter(names).WriteParameters(message);
		string localInvocation = message.ReceiveMode == NetworkMessageReceiveModeModel.Event
			? $"(({message.DeclaringTypeName}.{(message.Kind == NetworkMessageKind.Rpc ? "RPC" : "Broadcast")})this).Raise{message.Name}({names.Client}, {names.Instigator}{ReceiveForwardedArguments(message)});"
			: $"(({message.DeclaringTypeName}.{(message.Kind == NetworkMessageKind.Rpc ? "RPC" : "Broadcast")})this).{message.Identifier}({names.Client}, {names.Instigator}{ReceiveForwardedArguments(message)});";

		if (message.Kind == NetworkMessageKind.Broadcast) {
			return $$"""
				public partial void {{message.Identifier}}({{parameterDeclarations}})
				{
					if (this.Peer is not global::Cat.Network.RelayClient {{names.Client}}) {
						throw new global::System.InvalidOperationException("Broadcasts can only be invoked by a connected relay client.");
					}
					if (!this.IsOwner) {
						throw new global::System.InvalidOperationException("Broadcasts can only be invoked by the entity owner.");
					}
					global::Cat.Network.NetworkProfile {{names.Instigator}} = {{names.Client}}.Profile ?? throw new global::System.InvalidOperationException("Cannot invoke a broadcast before the local profile has been assigned.");
					{{localInvocation}}
					global::Cat.Network.BufferWriter {{names.Writer}} = {{names.Client}}.RentBroadcastMessageWriter(this, {{message.Id}}UL, out global::Cat.Network.SerializationContext {{names.Context}});
					{{writerBody}}
					{{names.Client}}.QueueRentedMessageWriter({{names.Writer}});
				}
				""";
		}

		return $$"""
			public partial void {{message.Identifier}}({{parameterDeclarations}})
			{
				if (this.Peer is not global::Cat.Network.RelayClient {{names.Client}}) {
					throw new global::System.InvalidOperationException("RPCs can only be invoked by a connected relay client.");
				}
				if (this.IsOwner) {
					global::Cat.Network.NetworkProfile {{names.Instigator}} = {{names.Client}}.Profile ?? throw new global::System.InvalidOperationException("Cannot invoke an RPC before the local profile has been assigned.");
					{{localInvocation}}
					return;
				}
				global::Cat.Network.BufferWriter {{names.Writer}} = {{names.Client}}.RentRpcMessageWriter(this, {{message.Id}}UL, out global::Cat.Network.SerializationContext {{names.Context}});
				{{writerBody}}
				{{names.Client}}.QueueRentedMessageWriter({{names.Writer}});
			}
			""";
	}

	private static string MessageDispatch(NetworkObjectTypeModel model) {
		if (model.Rpcs.IsEmpty && model.Broadcasts.IsEmpty) {
			return string.Empty;
		}

		MessageScope names = new(model.Rpcs.Concat(model.Broadcasts).SelectMany(static message => message.Parameters).Select(static parameter => parameter.Name));

		return $$"""
			bool global::Cat.Network.INetworkRpcTarget.TryInvokeRpc(global::Cat.Network.RelayClient {{names.Client}}, global::Cat.Network.NetworkProfile {{names.Instigator}}, ulong {{names.RpcId}}, global::System.ReadOnlySpan<byte> {{names.Data}}, global::Cat.Network.SerializationContext {{names.Context}})
			{
				switch ({{names.RpcId}}) {
					{{MessageDispatchCases(model.Rpcs, "RPC", names)}}
					default:
						return false;
				}
			}

			bool global::Cat.Network.INetworkRpcTarget.TryInvokeBroadcast(global::Cat.Network.RelayClient {{names.Client}}, global::Cat.Network.NetworkProfile {{names.Instigator}}, ulong {{names.RpcId}}, global::System.ReadOnlySpan<byte> {{names.Data}}, global::Cat.Network.SerializationContext {{names.Context}})
			{
				switch ({{names.RpcId}}) {
					{{MessageDispatchCases(model.Broadcasts, "Broadcast", names)}}
					default:
						return false;
				}
			}
			""";
	}

	private static string MessageDispatchCases(ImmutableArray<NetworkMessageMethodModel> messages, string interfaceName, MessageScope names) {
		return string.Join(
			"\n",
			messages.Select(message => MessageDispatchCase(message, interfaceName, names)));
	}

	private static string MessageDispatchCase(NetworkMessageMethodModel message, string interfaceName, MessageScope names) {
		string readParameters = new ParameterEmitter(names).ReadParameters(message);
		string invocation = message.ReceiveMode == NetworkMessageReceiveModeModel.Event
			? $"(({message.DeclaringTypeName}.{interfaceName})this).Raise{message.Name}({names.Client}, {names.Instigator}{ReceiveForwardedArguments(message)});"
			: $"(({message.DeclaringTypeName}.{interfaceName})this).{message.Identifier}({names.Client}, {names.Instigator}{ReceiveForwardedArguments(message)});";

		return $$"""
			case {{message.Id}}UL: {
				{{readParameters}}
				if (!{{names.Data}}.IsEmpty) {
					return false;
				}
				{{invocation}}
				return true;
			}
			""";
	}

	private sealed class ParameterEmitter(MessageScope scope) {
		public string WriteParameters(NetworkMessageMethodModel message) {
			return string.Join(
				"\n",
				message.Parameters.Select((parameter, index) => WriteParameter(parameter, index)));
		}

		private string WriteParameter(NetworkMessageParameterModel parameter, int index) {
			string lengthRange = scope.Names.Allocate($"__catNetworkParameter{index}LengthRange");
			string valueStart = scope.Names.Allocate($"__catNetworkParameter{index}ValueStart");

			return $$"""
				{
					global::System.Range {{lengthRange}} = {{scope.Writer}}.Reserve(4);
					int {{valueStart}} = {{scope.Writer}}.WrittenCount;
					{{SerializeParameterValue(parameter, parameter.Identifier)}}
					{{scope.Writer}}.WriteInt32({{lengthRange}}, {{scope.Writer}}.WrittenCount - {{valueStart}});
				}
				""";
		}

		public string ReadParameters(NetworkMessageMethodModel message) {
			return string.Join(
				"\n",
				message.Parameters.Select((parameter, index) => ReadParameter(parameter, index)));
		}

		private string ReadParameter(NetworkMessageParameterModel parameter, int index) {
			string byteCount = scope.Names.Allocate($"__catNetworkParameter{index}ByteCount");
			string valueData = scope.Names.Allocate($"__catNetworkParameter{index}ValueData");

			return $$"""
				{{parameter.TypeName}} {{parameter.Identifier}};
				{
					if ({{scope.Data}}.Length < 4) {
						return false;
					}
					int {{byteCount}} = global::System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian({{scope.Data}});
					{{scope.Data}} = {{scope.Data}}[4..];
					if ({{byteCount}} < 0 || {{scope.Data}}.Length < {{byteCount}}) {
						return false;
					}
					global::System.ReadOnlySpan<byte> {{valueData}} = {{scope.Data}}[..{{byteCount}}];
					{{DeserializeParameterValue(parameter, valueData, parameter.Identifier)}}
					if (!{{valueData}}.IsEmpty) {
						return false;
					}
					{{scope.Data}} = {{scope.Data}}[{{byteCount}}..];
				}
				""";
		}

		private string SerializeParameterValue(NetworkMessageParameterModel parameter, string valueExpression) {
			if (parameter.IsNullableValueType) {
				string currentValue = scope.Names.Allocate($"__catNetwork{parameter.Name}Value");
				return $$"""
					{{parameter.TypeName}} {{currentValue}} = {{valueExpression}};
					if (!{{currentValue}}.HasValue) {
						{{scope.Writer}}.WriteByte(0);
					} else {
						{{scope.Writer}}.WriteByte(1);
						{{SerializeNonNullableValue(parameter.SerializationKind, parameter.RuntimeTypeName, parameter.StructFields, currentValue + ".Value", stringLengthPrefixed: false)}}
					}
					""";
			}

			return SerializeNonNullableValue(parameter.SerializationKind, parameter.RuntimeTypeName, parameter.StructFields, valueExpression, stringLengthPrefixed: false);
		}

		private string SerializeNonNullableValue(NetworkPropertySerializationKind kind, string runtimeTypeName, ImmutableArray<NetworkStructFieldModel> structFields, string valueExpression, bool stringLengthPrefixed) {
			switch (kind) {
				case NetworkPropertySerializationKind.Boolean:
					return $"{scope.Writer}.WriteByte({valueExpression} ? (byte)1 : (byte)0);";
				case NetworkPropertySerializationKind.Byte:
					return $"{scope.Writer}.WriteByte({valueExpression});";
				case NetworkPropertySerializationKind.SByte:
					return $"{scope.Writer}.WriteByte(unchecked((byte){valueExpression}));";
				case NetworkPropertySerializationKind.Int16:
					return $"{scope.Writer}.WriteInt16({valueExpression});";
				case NetworkPropertySerializationKind.UInt16:
					return $"{scope.Writer}.WriteUInt16({valueExpression});";
				case NetworkPropertySerializationKind.Int32:
					return $"{scope.Writer}.WriteInt32({valueExpression});";
				case NetworkPropertySerializationKind.UInt32:
					return $"{scope.Writer}.WriteUInt32({valueExpression});";
				case NetworkPropertySerializationKind.Int64:
					return $"{scope.Writer}.WriteInt64({valueExpression});";
				case NetworkPropertySerializationKind.UInt64:
					return $"{scope.Writer}.WriteUInt64({valueExpression});";
				case NetworkPropertySerializationKind.Single:
					return $"{scope.Writer}.WriteSingle({valueExpression});";
				case NetworkPropertySerializationKind.Double:
					return $"{scope.Writer}.WriteDouble({valueExpression});";
				case NetworkPropertySerializationKind.String:
					return stringLengthPrefixed
						? $$"""
							{{scope.Writer}}.WriteLengthPrefixedUtf8({{valueExpression}} ?? throw new global::System.InvalidOperationException("String network message parameters cannot be null."));
							"""
						: $"{scope.Writer}.WriteUtf8({valueExpression} ?? throw new global::System.InvalidOperationException(\"String network message parameters cannot be null.\"));";
				case NetworkPropertySerializationKind.Guid:
					return $"{scope.Writer}.WriteGuid({valueExpression});";
				case NetworkPropertySerializationKind.Struct:
					return SerializeStructValue(runtimeTypeName, structFields, valueExpression);
				case NetworkPropertySerializationKind.NetworkObject:
					return SerializeNetworkObjectValue(valueExpression);
				default:
					return "throw new global::System.InvalidOperationException(\"Network message parameter type is not supported.\");";
			}
		}

		private string SerializeStructValue(string runtimeTypeName, ImmutableArray<NetworkStructFieldModel> structFields, string valueExpression) {
			string currentValue = scope.Names.Allocate("__catNetworkStructValue");
			string fieldBodies = string.Join(
				"\n\n",
				structFields.Select(field => SerializeStructFieldBody(field, currentValue, "__catNetworkStructField" + field.Name)));

			return $$"""
				{{runtimeTypeName}} {{currentValue}} = {{valueExpression}};
				{{fieldBodies}}
				""";
		}

		private string SerializeStructFieldBody(NetworkStructFieldModel field, string targetExpression, string fieldPath) {
			fieldPath = scope.Names.Allocate(fieldPath);
			return $$"""
				{
				{{SerializeStructFieldBlock(field, targetExpression, fieldPath)}}
				}
				""";
		}

		private string SerializeStructFieldBlock(NetworkStructFieldModel field, string targetExpression, string fieldPath) {
			if (field.IsNullableValueType) {
				if (field.SerializationKind == NetworkPropertySerializationKind.Struct) {
					string nestedAccessor = $"{fieldPath}.Value";
					string nestedBody = string.Join(
						"\n\n",
						field.StructFields.Select(nestedField => SerializeStructFieldBody(nestedField, nestedAccessor, fieldPath + "__" + nestedField.Name)));

					return $$"""
						{{field.TypeName}} {{fieldPath}} = {{targetExpression}}.{{field.Identifier}};
						if (!{{fieldPath}}.HasValue) {
							{{scope.Writer}}.WriteByte(0);
						} else {
							{{scope.Writer}}.WriteByte(1);
							{{nestedBody}}
						}
						""";
				}

				return $$"""
					{{field.TypeName}} {{fieldPath}} = {{targetExpression}}.{{field.Identifier}};
					if (!{{fieldPath}}.HasValue) {
						{{scope.Writer}}.WriteByte(0);
					} else {
						{{scope.Writer}}.WriteByte(1);
						{{SerializeNonNullableValue(field.SerializationKind, field.RuntimeTypeName, field.StructFields, fieldPath + ".Value", stringLengthPrefixed: true)}}
					}
					""";
			}

			if (field.SerializationKind == NetworkPropertySerializationKind.Struct) {
				string nestedBody = string.Join(
					"\n\n",
					field.StructFields.Select(nestedField => SerializeStructFieldBody(nestedField, fieldPath, fieldPath + "__" + nestedField.Name)));

				return $$"""
					{{field.RuntimeTypeName}} {{fieldPath}} = {{targetExpression}}.{{field.Identifier}};
					{{nestedBody}}
					""";
			}

			return SerializeNonNullableValue(field.SerializationKind, field.RuntimeTypeName, field.StructFields, $"{targetExpression}.{field.Identifier}", stringLengthPrefixed: true);
		}

		private string SerializeNetworkObjectValue(string valueExpression) {
			string networkObject = scope.Names.Allocate("networkObject");
			string serializer = scope.Names.Allocate("serializer");
			return $$"""
				if ({{valueExpression}} is null) {
					{{scope.Writer}}.WriteByte((byte)global::Cat.Network.NetworkObjectUpdateMode.Clear);
				} else {
					global::Cat.Network.NetworkObject {{networkObject}} = {{valueExpression}};
					if (!{{scope.Context}}.TypeCatalogue.TryFindSerializer({{networkObject}}.GetType(), out global::Cat.Network.INetworkObjectSerializer? {{serializer}})) {
						throw new global::System.InvalidOperationException($"Serializer for type '{{{networkObject}}.GetType().FullName}' is not registered.");
					}

					{{scope.Writer}}.WriteByte((byte)global::Cat.Network.NetworkObjectUpdateMode.Replace);
					{{scope.Writer}}.WriteGuid({{scope.DeclaringTypeName}}.GetNetworkObjectTypeId({{networkObject}}.GetType()));
					{{serializer}}.Serialize({{scope.Writer}}, {{networkObject}}, {{scope.Context}}, new global::Cat.Network.SerializationOptions(global::Cat.Network.MemberSelectionMode.All, global::Cat.Network.MemberIdentificationMode.Name));
				}
				""";
		}

		private string DeserializeParameterValue(NetworkMessageParameterModel parameter, string valueData, string targetName) {
			string hasValue = scope.Names.Allocate("__catNetworkHasValue");
			if (parameter.IsNullableValueType) {
				string localValue = scope.Names.Allocate($"__catNetwork{parameter.Name}Value");
				return $$"""
					if ({{valueData}}.Length < 1) {
						return false;
					}
					byte {{hasValue}} = {{valueData}}[0];
					{{valueData}} = {{valueData}}[1..];
					switch ({{hasValue}}) {
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

		private string DeserializeNonNullableValue(NetworkPropertySerializationKind kind, string runtimeTypeName, ImmutableArray<NetworkStructFieldModel> structFields, string valueData, string targetName, bool stringLengthPrefixed) {
			string stringByteCount = scope.Names.Allocate("__catNetworkStringByteCount");
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
							uint {{stringByteCount}} = global::System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian({{valueData}});
							{{valueData}} = {{valueData}}[4..];
							if ({{valueData}}.Length < {{stringByteCount}}) {
								return false;
							}
							{{targetName}} = global::System.Text.Encoding.UTF8.GetString({{valueData}}[..(int){{stringByteCount}}]);
							{{valueData}} = {{valueData}}[(int){{stringByteCount}}..];
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

		private string DeserializeBinaryPrimitive(string valueData, string targetName, string methodName, int byteLength) {
			return $$"""
				if ({{valueData}}.Length < {{byteLength}}) {
					return false;
				}
				{{targetName}} = global::System.Buffers.Binary.BinaryPrimitives.{{methodName}}({{valueData}});
				{{valueData}} = {{valueData}}[{{byteLength}}..];
				""";
		}

		private string DeserializeStructValue(string runtimeTypeName, ImmutableArray<NetworkStructFieldModel> structFields, string valueData, string targetName) {
			string fieldBodies = string.Join(
				"\n\n",
				structFields.Select(field => DeserializeStructFieldBody(field, valueData, targetName, "__catNetworkStructField" + field.Name)));

			return $$"""
				{{targetName}} = default;
				{{fieldBodies}}
				""";
		}

		private string DeserializeStructFieldBody(NetworkStructFieldModel field, string valueData, string targetExpression, string fieldPath) {
			fieldPath = scope.Names.Allocate(fieldPath);
			return $$"""
				{
				{{DeserializeStructFieldBlock(field, valueData, targetExpression, fieldPath)}}
				}
				""";
		}

		private string DeserializeStructFieldBlock(NetworkStructFieldModel field, string valueData, string targetExpression, string fieldPath) {
			string hasValue = scope.Names.Allocate($"has{field.Name}Value");
			if (field.IsNullableValueType) {
				if (field.SerializationKind == NetworkPropertySerializationKind.Struct) {
					string nestedBody = string.Join(
						"\n\n",
						field.StructFields.Select(nestedField => DeserializeStructFieldBody(nestedField, valueData, fieldPath, fieldPath + "__" + nestedField.Name)));

					return $$"""
						if ({{valueData}}.Length < 1) {
							return false;
						}
						byte {{hasValue}} = {{valueData}}[0];
						{{valueData}} = {{valueData}}[1..];
						switch ({{hasValue}}) {
							case 0:
								{{targetExpression}}.{{field.Identifier}} = null;
								break;
							case 1:
								{{field.RuntimeTypeName}} {{fieldPath}} = default;
								{{nestedBody}}
								{{targetExpression}}.{{field.Identifier}} = {{fieldPath}};
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
					byte {{hasValue}} = {{valueData}}[0];
					{{valueData}} = {{valueData}}[1..];
					switch ({{hasValue}}) {
						case 0:
							{{targetExpression}}.{{field.Identifier}} = null;
							break;
						case 1:
							{{field.RuntimeTypeName}} {{fieldPath}};
							{{DeserializeNonNullableValue(field.SerializationKind, field.RuntimeTypeName, field.StructFields, valueData, fieldPath, stringLengthPrefixed: true)}}
							{{targetExpression}}.{{field.Identifier}} = {{fieldPath}};
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
					{{targetExpression}}.{{field.Identifier}} = {{fieldPath}};
					""";
			}

			return DeserializeNonNullableValue(field.SerializationKind, field.RuntimeTypeName, field.StructFields, valueData, $"{targetExpression}.{field.Identifier}", stringLengthPrefixed: true);
		}

		private string DeserializeNetworkObjectValue(string runtimeTypeName, string valueData, string targetName) {
			string updateMode = scope.Names.Allocate("__catNetworkObjectUpdateMode");
			string typeId = scope.Names.Allocate("__catNetworkReplacementTypeId");
			string type = scope.Names.Allocate("__catNetworkReplacementType");
			string serializer = scope.Names.Allocate("__catNetworkReplacementSerializer");
			string target = scope.Names.Allocate("__catNetworkReplacementTarget");
			return $$"""
				if ({{valueData}}.Length < 1) {
					return false;
				}
				global::Cat.Network.NetworkObjectUpdateMode {{updateMode}} = (global::Cat.Network.NetworkObjectUpdateMode){{valueData}}[0];
				{{valueData}} = {{valueData}}[1..];
				switch ({{updateMode}}) {
					case global::Cat.Network.NetworkObjectUpdateMode.Clear:
						{{targetName}} = null!;
						break;
					case global::Cat.Network.NetworkObjectUpdateMode.Replace:
						if ({{valueData}}.Length < 16) {
							return false;
						}
						global::System.Guid {{typeId}} = new global::System.Guid({{valueData}}[..16]);
						{{valueData}} = {{valueData}}[16..];
						if (!{{scope.Context}}.TypeCatalogue.TryFindType({{typeId}}, out global::System.Type? {{type}})) {
							return false;
						}
						if (!typeof({{runtimeTypeName}}).IsAssignableFrom({{type}})) {
							return false;
						}
						if (!{{scope.Context}}.TypeCatalogue.TryFindSerializer({{type}}, out global::Cat.Network.INetworkObjectSerializer? {{serializer}})) {
							return false;
						}
						if (global::System.Activator.CreateInstance({{type}}) is not {{runtimeTypeName}} {{target}}) {
							return false;
						}
						{{serializer}}.Deserialize({{target}}, {{valueData}}, {{scope.Context}});
						{{targetName}} = {{target}};
						{{valueData}} = global::System.ReadOnlySpan<byte>.Empty;
						break;
					default:
						return false;
				}
				""";
		}

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
		MessageScope names = new(message.Parameters.Select(static parameter => parameter.Name));
		IEnumerable<string> parameters = new[] {
				$"global::Cat.Network.RelayClient {names.Client}",
				$"global::Cat.Network.NetworkProfile {names.Instigator}"
			}
			.Concat(message.Parameters.Select(static parameter => $"{parameter.TypeName} {parameter.Identifier}"));
		return string.Join(", ", parameters);
	}

	private static string ReceiveArgumentNames(NetworkMessageMethodModel message) {
		MessageScope names = new(message.Parameters.Select(static parameter => parameter.Name));
		return string.Join(", ", new[] { names.Client, names.Instigator }.Concat(message.Parameters.Select(static parameter => parameter.Identifier)));
	}

	private static string ReceiveForwardedArguments(NetworkMessageMethodModel message) {
		if (message.Parameters.IsEmpty) {
			return string.Empty;
		}

		return ", " + string.Join(", ", message.Parameters.Select(static parameter => parameter.Identifier));
	}

	private sealed class MessageScope {
		public GeneratedNames Names { get; }
		public string Client { get; }
		public string Instigator { get; }
		public string Writer { get; }
		public string Context { get; }
		public string Data { get; }
		public string RpcId { get; }
		public string DeclaringTypeName { get; }

		public MessageScope(IEnumerable<string> parameters, string declaringTypeName = "") {
			Names = new GeneratedNames(parameters);
			Client = Names.Allocate("client");
			Instigator = Names.Allocate("instigator");
			Writer = Names.Allocate("writer");
			Context = Names.Allocate("context");
			Data = Names.Allocate("data");
			RpcId = Names.Allocate("rpcId");
			DeclaringTypeName = declaringTypeName;
		}
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
