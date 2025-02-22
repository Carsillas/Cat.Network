﻿using Microsoft.CodeAnalysis;
using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data.SqlTypes;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;

// @formatter:csharp_max_line_length 800

namespace Cat.Network.Generator {
	internal static class Utils {
		public static SymbolDisplayFormat FullyQualifiedFormat { get; } = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces, genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);
		public static SymbolDisplayFormat TypeNameFormat { get; } = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly, genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);
		public static SymbolDisplayFormat InterfaceMethodDeclarationFormat { get; } = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces, parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeDefaultValue | SymbolDisplayParameterOptions.IncludeName, memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType);
		public static SymbolDisplayFormat ClassMethodInvocationFormat { get; } = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces, parameterOptions: SymbolDisplayParameterOptions.IncludeName, memberOptions: SymbolDisplayMemberOptions.IncludeParameters);


		public const string BinaryPrimitivesFQN = "System.Buffers.Binary.BinaryPrimitives";
		public const string UnicodeFQN = "System.Text.Encoding.Unicode";
		public const string NetworkPropertyChangedEventAttributeFQN = "Cat.Network.PropertyChangedEventAttribute";
		public const string FixedSizeAttributeFQN = "Cat.Network.Collections.FixedSizeAttribute";
		public const string NetworkPropertyChangedEventFQN = "Cat.Network.NetworkPropertyChanged";
		public const string NetworkPropertyChangedEventArgsFQN = "Cat.Network.PropertyChangedEventArgs";
		public const string NetworkEntityInterfaceFQN = "Cat.Network.INetworkEntity";
		public const string NetworkSerializableInterfaceFQN = "Cat.Network.INetworkSerializable";
		public const string NetworkDataObjectInterfaceFQN = "Cat.Network.INetworkDataObject";
		public const string NetworkPropertyInfoFQN = "Cat.Network.NetworkPropertyInfo";
		public const string SerializationOptionsFQN = "Cat.Network.SerializationOptions";
		public const string CreateSerializationOptions = "Cat.Network.SerializationUtils.CreateOptions";
		public const string UpdateSerializationOptions = "Cat.Network.SerializationUtils.UpdateOptions";
		public const string MemberIdentifierModeFQN = "Cat.Network.MemberIdentifierMode";
		public const string MemberSelectionModeFQN = "Cat.Network.MemberSelectionMode";
		public const string MemberSerializationModeFQN = "Cat.Network.MemberSerializationMode";
		public const string GuidFQN = "System.Guid";
		public const string SpanFQN = "System.Span<byte>";
		public const string ReadOnlySpanFQN = "System.ReadOnlySpan<byte>";
		public const string NetworkEntityFQN = "Cat.Network.NetworkEntity";
		public const string NetworkDataObjectFQN = "Cat.Network.NetworkDataObject";
		public const string NetworkPropertyPrefix = "NetworkProperty";
		public const string NetworkPropertyPrefixAndDot = NetworkPropertyPrefix + ".";
		public const string NetworkCollectionPrefix = "NetworkCollection";
		public const string NetworkCollectionPrefixAndDot = NetworkCollectionPrefix + ".";
		public const string RpcPrefix = "RPC";
		public const string RpcPrefixAndDot = RpcPrefix + ".";

		public const string ForwardedAttributeFQN = "Cat.Network.ForwardedAttribute";
		public const string ClientParameterAttributeFQN = "Cat.Network.ClientAttribute";
		public const string InstigatorParameterAttributeFQN = "Cat.Network.InstigatorAttribute";
		public const string AutoEventAttributeFQN = "Cat.Network.AutoEventAttribute";


		public const string NetworkCollectionInterfaceFQN = "Cat.Network.Collections.INetworkCollection";
		public const string NetworkCollectionOperationFQN = "Cat.Network.Collections.NetworkCollectionOperation";
		public const string NetworkCollectionOperationTypeFQN = "Cat.Network.Collections.NetworkCollectionOperationType";
		public const string NetworkValueListFQN = "Cat.Network.Collections.NetworkValueList";
		public const string NetworkObjectListFQN = "Cat.Network.Collections.NetworkObjectList";


		private const string PropertyBufferName = "propertyBuffer";


		private static Dictionary<string, StringTemplate> SerializationTemplates { get; } = new Dictionary<string, StringTemplate>() {
			{ "System.Byte", new StringTemplate($"{PropertyBufferName}[0] = {{name}}; {PropertyBufferName} = {PropertyBufferName}.Slice(1);", "name") },
			{ "System.Int16", new StringTemplate($"{BinaryPrimitivesFQN}.WriteInt16LittleEndian({PropertyBufferName}, {{name}}); {PropertyBufferName} = {PropertyBufferName}.Slice(2);", "name") },
			{ "System.Int32", new StringTemplate($"{BinaryPrimitivesFQN}.WriteInt32LittleEndian({PropertyBufferName}, {{name}}); {PropertyBufferName} = {PropertyBufferName}.Slice(4);", "name") },
			{ "System.Int64", new StringTemplate($"{BinaryPrimitivesFQN}.WriteInt64LittleEndian({PropertyBufferName}, {{name}}); {PropertyBufferName} = {PropertyBufferName}.Slice(8);", "name") },
			{ "System.UInt16", new StringTemplate($"{BinaryPrimitivesFQN}.WriteUInt16LittleEndian({PropertyBufferName}, {{name}}); {PropertyBufferName} = {PropertyBufferName}.Slice(2);", "name") },
			{ "System.UInt32", new StringTemplate($"{BinaryPrimitivesFQN}.WriteUInt32LittleEndian({PropertyBufferName}, {{name}}); {PropertyBufferName} = {PropertyBufferName}.Slice(4);", "name") },
			{ "System.UInt64", new StringTemplate($"{BinaryPrimitivesFQN}.WriteUInt64LittleEndian({PropertyBufferName}, {{name}}); {PropertyBufferName} = {PropertyBufferName}.Slice(8);", "name") },
			{ "System.Single", new StringTemplate($"{BinaryPrimitivesFQN}.WriteSingleLittleEndian({PropertyBufferName}, {{name}}); {PropertyBufferName} = {PropertyBufferName}.Slice(4);", "name") },
			{ "System.Double", new StringTemplate($"{BinaryPrimitivesFQN}.WriteDoubleLittleEndian({PropertyBufferName}, {{name}}); {PropertyBufferName} = {PropertyBufferName}.Slice(8);", "name") },
			{ "System.Boolean", new StringTemplate($"{PropertyBufferName}[0] = {{name}} ? (byte) 1 : (byte) 0; {PropertyBufferName} = {PropertyBufferName}.Slice(1);", "name") },
			{ "System.String", new StringTemplate($"System.Int32 serializedStringLength = {UnicodeFQN}.GetBytes({{name}}, {PropertyBufferName}.Slice(4)); {BinaryPrimitivesFQN}.WriteInt32LittleEndian({PropertyBufferName}, serializedStringLength); {PropertyBufferName} = {PropertyBufferName}.Slice(4 + serializedStringLength);", "name") { IsReference = true } },
			{ "System.Guid", new StringTemplate($"{{name}}.TryWriteBytes({PropertyBufferName}); {PropertyBufferName} = {PropertyBufferName}.Slice(16);", "name") },
			{ "System.Collections.Immutable.ImmutableArray<System.Byte>", new StringTemplate($"{PropertyBufferName}[0] = ({{name}}.IsDefault ? (byte) 0 : (byte) 1); {PropertyBufferName} = {PropertyBufferName}.Slice(1); if (!{{name}}.IsDefault) {{ {BinaryPrimitivesFQN}.WriteInt32LittleEndian({PropertyBufferName}, {{name}}.Length); {PropertyBufferName} = {PropertyBufferName}.Slice(4); {{name}}.CopyTo({PropertyBufferName}); {PropertyBufferName} = {PropertyBufferName}.Slice({{name}}.Length); }}", "name") }
		};

		private static Dictionary<string, StringTemplate> DeserializationTemplates { get; } = new() {
			{ "System.Byte", new StringTemplate($"{{name}} = {PropertyBufferName}[0]; {PropertyBufferName} = {PropertyBufferName}.Slice(1);", "name") },
			{ "System.Int16", new StringTemplate($"{{name}} = {BinaryPrimitivesFQN}.ReadInt16LittleEndian({PropertyBufferName}); {PropertyBufferName} = {PropertyBufferName}.Slice(2);", "name") },
			{ "System.Int32", new StringTemplate($"{{name}} = {BinaryPrimitivesFQN}.ReadInt32LittleEndian({PropertyBufferName}); {PropertyBufferName} = {PropertyBufferName}.Slice(4);", "name") },
			{ "System.Int64", new StringTemplate($"{{name}} = {BinaryPrimitivesFQN}.ReadInt64LittleEndian({PropertyBufferName}); {PropertyBufferName} = {PropertyBufferName}.Slice(8);", "name") },
			{ "System.UInt16", new StringTemplate($"{{name}} = {BinaryPrimitivesFQN}.ReadUInt16LittleEndian({PropertyBufferName}); {PropertyBufferName} = {PropertyBufferName}.Slice(2);", "name") },
			{ "System.UInt32", new StringTemplate($"{{name}} = {BinaryPrimitivesFQN}.ReadUInt32LittleEndian({PropertyBufferName}); {PropertyBufferName} = {PropertyBufferName}.Slice(4);", "name") },
			{ "System.UInt64", new StringTemplate($"{{name}} = {BinaryPrimitivesFQN}.ReadUInt64LittleEndian({PropertyBufferName}); {PropertyBufferName} = {PropertyBufferName}.Slice(8);", "name") },
			{ "System.Single", new StringTemplate($"{{name}} = {BinaryPrimitivesFQN}.ReadSingleLittleEndian({PropertyBufferName}); {PropertyBufferName} = {PropertyBufferName}.Slice(4);", "name") },
			{ "System.Double", new StringTemplate($"{{name}} = {BinaryPrimitivesFQN}.ReadDoubleLittleEndian({PropertyBufferName}); {PropertyBufferName} = {PropertyBufferName}.Slice(8);", "name") },
			{ "System.Boolean", new StringTemplate($"{{name}} = {PropertyBufferName}[0] == 1; {PropertyBufferName} = {PropertyBufferName}.Slice(1);", "name") },
			{ "System.String", new StringTemplate($"System.Int32 serializedStringLength = {BinaryPrimitivesFQN}.ReadInt32LittleEndian({PropertyBufferName}); {{name}} = {UnicodeFQN}.GetString({PropertyBufferName}.Slice(4, serializedStringLength)); {PropertyBufferName} = {PropertyBufferName}.Slice(4 + serializedStringLength);", "name") { IsReference = true } },
			{ "System.Guid", new StringTemplate($"{{name}} = new System.Guid({PropertyBufferName}); {PropertyBufferName} = {PropertyBufferName}.Slice(16);", "name") },
			{ "System.Collections.Immutable.ImmutableArray<System.Byte>", new StringTemplate($"if ({PropertyBufferName}[0] == 1) {{ {PropertyBufferName} = {PropertyBufferName}.Slice(1); System.Int32 immutableArrayLength = {BinaryPrimitivesFQN}.ReadInt32LittleEndian({PropertyBufferName}); {PropertyBufferName} = {PropertyBufferName}.Slice(4); {{name}} = System.Collections.Immutable.ImmutableArray.Create<byte>({PropertyBufferName}.Slice(0, immutableArrayLength)); {PropertyBufferName} = {PropertyBufferName}.Slice(immutableArrayLength); }} else {{ {{name}} = default; }}", "name") }
		};


		private static Dictionary<SpecialType, StringTemplate> EnumSerializationTemplates { get; } =
			new() {
				{ SpecialType.System_Byte, new StringTemplate($"{PropertyBufferName}[0] = (System.Byte){{name}}; {PropertyBufferName} = {PropertyBufferName}.Slice(1);", "name") },
				{ SpecialType.System_Int16, new StringTemplate($"{BinaryPrimitivesFQN}.WriteInt16LittleEndian({PropertyBufferName}, (System.Int16){{name}}); {PropertyBufferName} = {PropertyBufferName}.Slice(2);", "name") },
				{ SpecialType.System_Int32, new StringTemplate($"{BinaryPrimitivesFQN}.WriteInt32LittleEndian({PropertyBufferName}, (System.Int32){{name}}); {PropertyBufferName} = {PropertyBufferName}.Slice(4);", "name") },
				{ SpecialType.System_Int64, new StringTemplate($"{BinaryPrimitivesFQN}.WriteInt64LittleEndian({PropertyBufferName}, (System.Int64){{name}}); {PropertyBufferName} = {PropertyBufferName}.Slice(8);", "name") },
				{ SpecialType.System_UInt16, new StringTemplate($"{BinaryPrimitivesFQN}.WriteUInt16LittleEndian({PropertyBufferName}, (System.UInt16){{name}}); {PropertyBufferName} = {PropertyBufferName}.Slice(2);", "name") },
				{ SpecialType.System_UInt32, new StringTemplate($"{BinaryPrimitivesFQN}.WriteUInt32LittleEndian({PropertyBufferName}, (System.UInt32){{name}}); {PropertyBufferName} = {PropertyBufferName}.Slice(4);", "name") },
				{ SpecialType.System_UInt64, new StringTemplate($"{BinaryPrimitivesFQN}.WriteUInt64LittleEndian({PropertyBufferName}, (System.UInt64){{name}}); {PropertyBufferName} = {PropertyBufferName}.Slice(8);", "name") },
			};

		private static Dictionary<SpecialType, StringTemplate> EnumDeserializationTemplates { get; } =
			new() {
				{ SpecialType.System_Byte, new StringTemplate($"{{name}} = ({{enumFQN}}){PropertyBufferName}[0]; {PropertyBufferName} = {PropertyBufferName}.Slice(1);", "name", "enumFQN") },
				{ SpecialType.System_Int16, new StringTemplate($"{{name}} = ({{enumFQN}}){BinaryPrimitivesFQN}.ReadInt16LittleEndian({PropertyBufferName}); {PropertyBufferName} = {PropertyBufferName}.Slice(2);", "name", "enumFQN") },
				{ SpecialType.System_Int32, new StringTemplate($"{{name}} = ({{enumFQN}}){BinaryPrimitivesFQN}.ReadInt32LittleEndian({PropertyBufferName}); {PropertyBufferName} = {PropertyBufferName}.Slice(4);", "name", "enumFQN") },
				{ SpecialType.System_Int64, new StringTemplate($"{{name}} = ({{enumFQN}}){BinaryPrimitivesFQN}.ReadInt64LittleEndian({PropertyBufferName}); {PropertyBufferName} = {PropertyBufferName}.Slice(8);", "name", "enumFQN") },
				{ SpecialType.System_UInt16, new StringTemplate($"{{name}} = ({{enumFQN}}){BinaryPrimitivesFQN}.ReadUInt16LittleEndian({PropertyBufferName}); {PropertyBufferName} = {PropertyBufferName}.Slice(2);", "name", "enumFQN") },
				{ SpecialType.System_UInt32, new StringTemplate($"{{name}} = ({{enumFQN}}){BinaryPrimitivesFQN}.ReadUInt32LittleEndian({PropertyBufferName}); {PropertyBufferName} = {PropertyBufferName}.Slice(4);", "name", "enumFQN") },
				{ SpecialType.System_UInt64, new StringTemplate($"{{name}} = ({{enumFQN}}){BinaryPrimitivesFQN}.ReadUInt64LittleEndian({PropertyBufferName}); {PropertyBufferName} = {PropertyBufferName}.Slice(8);", "name", "enumFQN") }
			};

		private static StringTemplate SerializationTemplateWrapper = new($"{{ {SpanFQN} {PropertyBufferName} = {{bufferName}}.Slice(4); {{serializationExpression}} System.Int32 lengthStorage = {{bufferName}}.Slice(4).Length - {PropertyBufferName}.Length; {BinaryPrimitivesFQN}.WriteInt32LittleEndian({{bufferName}}, lengthStorage); {{bufferName}} = {{bufferName}}.Slice(4 + lengthStorage); }}", "bufferName", "serializationExpression");
		private static StringTemplate DeserializationTemplateWrapper = new($"{{ {ReadOnlySpanFQN} {PropertyBufferName} = {{bufferName}}; {{serializationTemplate}} }}", "bufferName", "serializationTemplate");
		private static StringTemplate NullableSerializationTemplate = new($"{PropertyBufferName}[0] = {{propertyName}}.HasValue ? (byte) 1 : (byte) 0; {PropertyBufferName} = {PropertyBufferName}.Slice(1); if ({{propertyName}}.HasValue) {{ {{serializationExpression}} }}", "propertyName", "serializationExpression");
		private static StringTemplate NullableDeserializationTemplate = new($"if ({PropertyBufferName}[0] == 1) {{ {PropertyBufferName} = {PropertyBufferName}.Slice(1); {{deserializationExpression}} }} else {{ {{propertyName}} = null; }}", "propertyName", "deserializationExpression");
		private static StringTemplate NullableReferenceSerializationTemplate = new($"{PropertyBufferName}[0] = ({{propertyName}} != null ? (byte) 1 : (byte) 0); {PropertyBufferName} = {PropertyBufferName}.Slice(1); if ({{propertyName}} != null) {{ {{serializationExpression}} }}", "propertyName", "serializationExpression");
		private static StringTemplate NullableReferenceDeserializationTemplate = new($"if ({PropertyBufferName}[0] == 1) {{ {PropertyBufferName} = {PropertyBufferName}.Slice(1); {{deserializationExpression}} }} else {{ {{propertyName}} = null; }}", "propertyName", "deserializationExpression");
		
		public static string GenerateSerialization(string serializationExpression, string bufferName) {
			serializationExpression = SerializationTemplateWrapper.Apply(bufferName, serializationExpression);
			return serializationExpression;
		}

		public static string GenerateDeserialization(string deserializationExpression, string bufferName) {
			deserializationExpression = DeserializationTemplateWrapper.Apply(bufferName, deserializationExpression);
			return deserializationExpression;
		}

		public static bool PassNodesOfType<T>(SyntaxNode syntaxNode, CancellationToken cancellationToken) {
			if (syntaxNode is T) {
				return true;
			}

			return false;
		}

		public static bool IsTypeWithFQN(INamedTypeSymbol typeSymbol, string fqn) {
			INamedTypeSymbol currentSymbol = typeSymbol;

			while (currentSymbol != null) {
				if (currentSymbol.ToDisplayString(FullyQualifiedFormat) == fqn) {
					return true;
				}

				currentSymbol = currentSymbol.BaseType;
			}

			return false;
		}

		public static bool IsNullableValueType(ITypeSymbol symbol) {
			return IsNullableValueType(symbol, out _);
		}

		private static bool IsNullableValueType(ITypeSymbol symbol, out INamedTypeSymbol valueType) {
			valueType = null;
			if (symbol.OriginalDefinition.ToDisplayString(FullyQualifiedFormat) == "System.Nullable<T>" &&
			    symbol is INamedTypeSymbol namedSymbol &&
			    namedSymbol.TypeArguments.Length == 1 &&
			    namedSymbol.TypeArguments[0] is INamedTypeSymbol namedGenericArgument) {
				valueType = namedGenericArgument;
				return true;
			}

			return false;
		}

		public static string GenerateTypeSerialization(string name, ITypeSymbol symbol) {
			if (TryGetSerialization(symbol, string.Empty, name, out string serialization)) {
				return serialization;
			}

			return $"Failed to generate serialization for item {symbol.ToDisplayString(FullyQualifiedFormat)} {name}";
		}

		public static string GenerateTypeDeserialization(string name, ITypeSymbol symbol) {
			if (TryGetDeserialization(symbol, string.Empty, name, out string deserialization)) {
				return deserialization;
			}

			return $"Failed to generate deserialization for item {symbol.ToDisplayString(FullyQualifiedFormat)} {name}";
		}

		private static IEnumerable<ISymbol> GetSerializationEligibleSymbols(ITypeSymbol symbol) {
			if (IsNullableValueType(symbol, out INamedTypeSymbol valueType)) {
				symbol = valueType;
			}

			return symbol.GetMembers()
				.Where(member => !member.IsStatic)
				.Where(member => {

					switch (member) {
						case IPropertySymbol propertySymbol:
							bool isValueType = propertySymbol.Type.IsValueType;
							bool isStringType = propertySymbol.Type.SpecialType == SpecialType.System_String;
							bool isAutoProperty = symbol.GetMembers().Any(f => f is IFieldSymbol fieldSymbol && SymbolEqualityComparer.Default.Equals(fieldSymbol.AssociatedSymbol, propertySymbol));

							return isAutoProperty && (isValueType || isStringType);
						case IFieldSymbol fieldSymbol:
							isValueType = fieldSymbol.Type.IsValueType;
							isStringType = fieldSymbol.Type.SpecialType == SpecialType.System_String;

							return isValueType || isStringType;
					}

					return false;
				})
				.Where(s => s.DeclaredAccessibility == Accessibility.Public)
				.OrderBy(s => s.Name);
		}


		private static bool TryGetSerialization(ITypeSymbol symbol, string accessPrefix, string name, out string serialization) {
			return TryGetBuiltInSerialization(symbol, accessPrefix, name, out serialization) ||
			       TryGetCompoundSerialization(symbol, accessPrefix, name, out serialization);
		}


		private static bool TryGetDeserialization(ITypeSymbol symbol, string accessPrefix, string name, out string deserialization) {
			return TryGetBuiltInDeserialization(symbol, accessPrefix, name, out deserialization) ||
			       TryGetCompoundDeserialization(symbol, accessPrefix, name, out deserialization);
		}

		private static bool TryGetBuiltInSerialization(ITypeSymbol symbol, string accessPrefix, string name, out string serialization) {
			ITypeSymbol serializationType = symbol;
			bool isNullable = IsNullableValueType(symbol, out INamedTypeSymbol valueType);
			if (isNullable) {
				serializationType = valueType;
			}

			serialization = null;
			string accessName = accessPrefix + (isNullable ? $"{name}.Value" : name);

			if (serializationType.TypeKind == TypeKind.Enum && serializationType is INamedTypeSymbol namedTypeSymbol && EnumSerializationTemplates.TryGetValue(namedTypeSymbol.EnumUnderlyingType.SpecialType, out StringTemplate serializationTemplate)) {
				serialization = serializationTemplate.Apply(accessName);
			} else if (SerializationTemplates.TryGetValue(serializationType.ToDisplayString(FullyQualifiedFormat), out serializationTemplate)) {
				serialization = serializationTemplate.Apply(accessName);

				if (serializationTemplate.IsReference) {
					serialization = NullableReferenceSerializationTemplate.Apply(accessName, serialization);
				}
			}

			if (serialization != null) {
				serialization = isNullable ? NullableSerializationTemplate.Apply(name, serialization) : serialization;
				return true;
			}

			return false;
		}

		private static bool TryGetBuiltInDeserialization(ITypeSymbol symbol, string accessPrefix, string name, out string deserialization) {
			ITypeSymbol serializationType = symbol;
			bool isNullable = IsNullableValueType(symbol, out INamedTypeSymbol valueType);
			if (isNullable) {
				serializationType = valueType;
			}

			deserialization = null;
			string accessName = $"{accessPrefix}{(accessPrefix == string.Empty ? "" : ".")}{name}";

			if (serializationType.TypeKind == TypeKind.Enum && serializationType is INamedTypeSymbol namedTypeSymbol && EnumDeserializationTemplates.TryGetValue(namedTypeSymbol.EnumUnderlyingType.SpecialType, out StringTemplate deserializationTemplate)) {
				deserialization = deserializationTemplate.Apply(accessName, serializationType.ToDisplayString(FullyQualifiedFormat));
			} else if (DeserializationTemplates.TryGetValue(serializationType.ToDisplayString(FullyQualifiedFormat), out deserializationTemplate)) {
				deserialization = deserializationTemplate.Apply(accessName);
				
				if (deserializationTemplate.IsReference) {
					deserialization = NullableReferenceDeserializationTemplate.Apply(accessName, deserialization);
				}
			}

			if (deserialization != null) {
				deserialization = isNullable ? NullableDeserializationTemplate.Apply(accessName, deserialization) : deserialization;
				return true;
			}

			return false;
		}


		public static string GetReferenceSerialization(string name, ITypeSymbol symbol, bool complete) {

			ScopedStringWriter writer = new ScopedStringWriter();

			using (writer.EnterScope($"if (ReferenceEquals({name}, null))")) {
				writer.AppendLine($"{PropertyBufferName}[0] = (byte) 0; {PropertyBufferName} = {PropertyBufferName}.Slice(1);");
			}

			using (writer.EnterScope($"else")) {
				writer.AppendLine($"{PropertyBufferName}[0] = (byte) 1; {PropertyBufferName} = {PropertyBufferName}.Slice(1);");

				if (complete) {
					writer.AppendLine($"{PropertyBufferName}[0] = (byte) 1; {PropertyBufferName} = {PropertyBufferName}.Slice(1);");
					writer.AppendBlock(@$"
						System.Int32 serializedStringLength = {UnicodeFQN}.GetBytes({name}.GetType().AssemblyQualifiedName, {PropertyBufferName}.Slice(4));
						{BinaryPrimitivesFQN}.WriteInt32LittleEndian({PropertyBufferName}, serializedStringLength);
						{PropertyBufferName} = {PropertyBufferName}.Slice(4 + serializedStringLength);
					");
				} else {
					writer.AppendLine($"{PropertyBufferName}[0] = (byte) 0; {PropertyBufferName} = {PropertyBufferName}.Slice(1);");
				}

				writer.AppendLine($"{PropertyBufferName} = {PropertyBufferName}.Slice((({NetworkSerializableInterfaceFQN}){name}).Serialize({(complete ? CreateSerializationOptions : UpdateSerializationOptions)}, {PropertyBufferName}));");
			}

			return writer.ToString();
		}

		public static string GetReferenceDeserialization(string name, ITypeSymbol symbol) {

			ScopedStringWriter writer = new ScopedStringWriter();

			writer.AppendBlock($@"
				System.Boolean hasValue = {PropertyBufferName}[0] == 1;
				{PropertyBufferName} = {PropertyBufferName}.Slice(1);
				if (hasValue) {{
					System.Boolean isComplete = {PropertyBufferName}[0] == 1;
					{PropertyBufferName} = {PropertyBufferName}.Slice(1);
					
					{symbol.ToDisplayString(FullyQualifiedFormat)} _{name} = {name};
					if (isComplete) {{
						System.Int32 serializedStringLength = {BinaryPrimitivesFQN}.ReadInt32LittleEndian({PropertyBufferName});
						System.String assemblyQualifiedName = {UnicodeFQN}.GetString({PropertyBufferName}.Slice(4, serializedStringLength));
						{PropertyBufferName} = {PropertyBufferName}.Slice(4 + serializedStringLength);
						System.Type type = System.Type.GetType(assemblyQualifiedName);
						
 						_{name} = ({symbol.ToDisplayString(FullyQualifiedFormat)}) System.Activator.CreateInstance(type);
					}}

					System.Int32 referencePropertyLength = {BinaryPrimitivesFQN}.ReadInt32LittleEndian({PropertyBufferName}); 
					{PropertyBufferName} = {PropertyBufferName}.Slice(4);

					(({NetworkSerializableInterfaceFQN})_{name}).Deserialize(serializationOptions, {PropertyBufferName}.Slice(0, referencePropertyLength));
					
					if (!ReferenceEquals({name}, _{name})) {{
						{name} = _{name};
					}}					
				}} else {{
					{name} = null;
				}}

			");
			
			

			return writer.ToString();
		}


		private static bool TryGetCompoundSerialization(ITypeSymbol symbol, string accessPrefix, string name, out string serialization) {
			bool isNullable = IsNullableValueType(symbol, out INamedTypeSymbol valueType);

			if (isNullable) {
				symbol = valueType;
			}

			IEnumerable<ISymbol> memberSymbols = GetSerializationEligibleSymbols(symbol);

			ScopedStringWriter writer = new ScopedStringWriter();

			using (writer.EnterScope()) {
				writer.AppendLine($"// {name} : {symbol.ToDisplayString(FullyQualifiedFormat)}");

				foreach (ISymbol member in memberSymbols) {
					ITypeSymbol memberType = (member as IFieldSymbol)?.Type ?? (member as IPropertySymbol)?.Type;

					string nextAccessPrefix = $"{accessPrefix}{name}.{(isNullable ? "Value." : "")}";
					if (memberType != null && TryGetSerialization(memberType, nextAccessPrefix, member.Name, out serialization)) {
						serialization = string.Join("\n", serialization.Split('\n').Select(x => $"\t{x}"));

						writer.AppendLine(serialization);
					}
				}
			}

			serialization = writer.ToString();

			if (isNullable) {
				serialization = NullableSerializationTemplate.Apply($"{accessPrefix}{name}", serialization);
			}

			return true;
		}

		private static bool TryGetCompoundDeserialization(ITypeSymbol symbol, string accessPrefix, string name, out string deserialization) {
			bool isNullable = IsNullableValueType(symbol, out INamedTypeSymbol valueType);

			if (isNullable) {
				symbol = valueType;
			}

			IEnumerable<ISymbol> memberSymbols = GetSerializationEligibleSymbols(symbol);

			ScopedStringWriter writer = new ScopedStringWriter();

			string localValueName = $"{accessPrefix}{name}_";
			string accessName = $"{accessPrefix}{(accessPrefix == string.Empty ? "" : ".")}{name}";

			using (writer.EnterScope()) {
				writer.AppendLine($"// {name}");
				writer.AppendLine($"{symbol.ToDisplayString(FullyQualifiedFormat)} {localValueName} = default;");

				foreach (ISymbol member in memberSymbols) {
					ITypeSymbol memberType = (member as IFieldSymbol)?.Type ?? (member as IPropertySymbol)?.Type;

					if (memberType != null && TryGetDeserialization(memberType, localValueName, member.Name, out deserialization)) {
						deserialization = string.Join("\n", deserialization.Split('\n').Select(x => $"\t{x}"));

						writer.AppendBlock(deserialization);
					}
				}

				writer.AppendLine($"{accessName} = {localValueName};");
			}

			deserialization = writer.ToString();

			if (isNullable) {
				deserialization = NullableDeserializationTemplate.Apply(accessName, deserialization);
			}

			return true;
		}

		public static TypeInfo GetTypeInfo(ITypeSymbol type) {
			return new TypeInfo {
				IsNullable = IsNullableValueType(type),
				GenericArgumentFQNs = (type as INamedTypeSymbol)?.TypeArguments
					.Select(t => t.ToDisplayString(FullyQualifiedFormat)).ToImmutableArray() ?? ImmutableArray<string>.Empty,
				FullyQualifiedTypeName = type.ToDisplayString(FullyQualifiedFormat),
				IsNetworkDataObject = type is INamedTypeSymbol namedTypeSymbol && IsTypeWithFQN(namedTypeSymbol, NetworkDataObjectFQN)
			};
		}
	}
}