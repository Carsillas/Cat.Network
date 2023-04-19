using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;

using static Cat.Network.Generator.GeneratorUtils;
using static Cat.Network.Generator.RPCGenerator;

namespace Cat.Network.Generator {

	[Generator(LanguageNames.CSharp)]
	public class NetworkPropertyGenerator : IIncrementalGenerator {

		public const string NetworkPropertyAttributeMetadataName = "Cat.Network.Generator.NetworkPropertyAttribute";
		public const string ConditionalAttributeMetadataName = "Cat.Network.RPCs";
		public const string NetworkEntityMetadataName = "Cat.Network.Entities.NetworkEntity";

		public const string NetworkPropertyPrefix = "NetworkProp";
		public const string NetworkPropertyPrefixAndDot = NetworkPropertyPrefix + ".";

		private static SymbolDisplayFormat FullyQualifiedFormat { get; } = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces, genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);
		private static SymbolDisplayFormat SimplyQualifiedFormat { get; } = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes, genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);
		private static SymbolDisplayFormat TypeNameFormat { get; } = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly, genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);


		public void Initialize(IncrementalGeneratorInitializationContext context) {

			IncrementalValuesProvider<NetworkEntityClassDefinition> allClasses = context.SyntaxProvider.CreateSyntaxProvider(
				PassNodesOfType<ClassDeclarationSyntax>(),
				(generatorSyntaxContext, cancellationToken) => {

					ClassDeclarationSyntax node = (ClassDeclarationSyntax)generatorSyntaxContext.Node;
					INamedTypeSymbol symbol = generatorSyntaxContext.SemanticModel.GetDeclaredSymbol(node);

					var builder = ImmutableArray.CreateBuilder<PropertyData>();
					bool isNetworkEntity = false;
					INamedTypeSymbol currentSymbol = symbol;

					while (currentSymbol != null) {

						builder.AddRange(GetPropertyDatasForSymbol(currentSymbol));

						if (currentSymbol.ToDisplayString(FullyQualifiedFormat) == NetworkEntityMetadataName) {
							isNetworkEntity = true;
							break;
						}
						currentSymbol = currentSymbol.BaseType;
					}
					builder.Reverse();

					if (!isNetworkEntity) {
						return new NetworkEntityClassDefinition { IsNetworkEntity = false };
					}

					return new NetworkEntityClassDefinition {
						Name = symbol.ToDisplayString(TypeNameFormat),
						IsNetworkEntity = true,
						Namespace = symbol.ContainingNamespace.ToDisplayString(FullyQualifiedFormat),
						MetadataName = symbol.MetadataName,
						NetworkProperties = builder.ToImmutableArray(),
						DeclaredNetworkProperties = GetPropertyDatasForNode(generatorSyntaxContext, node).Reverse().ToImmutableArray()
					};

					static IEnumerable<PropertyData> GetPropertyDatasForSymbol(INamedTypeSymbol typeSymbol) {

						return typeSymbol
						.GetMembers()
						.OfType<IPropertySymbol>()
						.Where(s => {
							int lastDot = s.Name.LastIndexOf('.');
							if (lastDot > 0) {
								return s.Name.Substring(0, lastDot).Split('.').LastOrDefault() == NetworkPropertyPrefix;
							}
							return false;
						})
						.OrderByDescending(propertySymbol => propertySymbol.Name)
						.Select(propertySymbol => new PropertyData {
							Name = propertySymbol.Name.Split('.').Last(),
							AccessModifier = 0,
							FullyQualifiedTypeName = propertySymbol.Type.ToDisplayString(FullyQualifiedFormat)
						});
					}

					static IEnumerable<PropertyData> GetPropertyDatasForNode(GeneratorSyntaxContext generatorSyntaxContext, ClassDeclarationSyntax node) {
						return node.ChildNodes()
							.OfType<PropertyDeclarationSyntax>()
							.Where(propertySyntax => propertySyntax.ChildNodes().Any(child => child is ExplicitInterfaceSpecifierSyntax explicitSpecifier &&
								explicitSpecifier.DescendantNodes().OfType<IdentifierNameSyntax>().LastOrDefault()?.Identifier.Text == NetworkPropertyPrefix))
						.OrderByDescending(propertySyntax => propertySyntax.Identifier.Text)
						.Select(propertySyntax => new PropertyData {
							Name = propertySyntax.Identifier.Text,
							AccessModifier = 0,
							FullyQualifiedTypeName = generatorSyntaxContext.SemanticModel.GetDeclaredSymbol(propertySyntax).Type.ToDisplayString(FullyQualifiedFormat)
						});
					}
				}
			);


			IncrementalValuesProvider<NetworkEntityClassDefinition> allNetworkEntities =
				allClasses.Where(data => data.IsNetworkEntity);


			context.RegisterSourceOutput(allNetworkEntities,
				(c, source) => c.AddSource($"{source.Namespace}.{source.MetadataName}.NetworkProperties", source.GenerateSource()));
		}



		public struct NetworkEntityClassDefinition {

			public string Namespace { get; set; }
			public string Name { get; set; }
			public string MetadataName { get; set; }

			public bool IsNetworkEntity { get; set; }

			public ImmutableArray<PropertyData> NetworkProperties { get; set; }
			public ImmutableArray<PropertyData> DeclaredNetworkProperties { get; set; }


			private const string NetworkEntityInterfaceFQN = "Cat.Network.Generator.INetworkEntity";
			private const string NetworkPropertyInfoFQN = "Cat.Network.Properties.NetworkPropertyInfo";
			private const string SerializationOptionsFQN = "Cat.Network.Serialization.SerializationOptions";
			private const string SpanFQN = "System.Span<byte>";
			private const string ReadOnlySpanFQN = "System.ReadOnlySpan<byte>";
			public const string UnsafeFQN = "System.Runtime.CompilerServices.Unsafe";


			public string GenerateSource() {

				return $@"

// {NetworkProperties.Length}
// {DeclaredNetworkProperties.Length}

namespace {Namespace} {{

	partial class {Name} : {NetworkEntityInterfaceFQN}, {Name}.{NetworkPropertyPrefix} {{

{GenerateInterface()}
{GenerateProperties()}
{GenerateInitializer()}
{GenerateSerialize()}
{GenerateDeserialize()}
{GenerateClean()}

	}}

}}

";

			}


			private string GenerateInterface() {
				return $@"
		private interface {NetworkPropertyPrefix} {{
{GenerateInterfaceProperties()}
		}}
";
			}

			private string GenerateInterfaceProperties() {

				StringBuilder stringBuilder = new StringBuilder();

				foreach (PropertyData property in DeclaredNetworkProperties) {
					stringBuilder.AppendLine($"\t\t\t{property.InterfacePropertyDeclaration}");
				}

				return stringBuilder.ToString();

			}

			private string GenerateProperties() {

				StringBuilder stringBuilder = new StringBuilder();

				int declaredPropertiesStartIndex = NetworkProperties.Length - DeclaredNetworkProperties.Length;

				for (int i = 0; i < DeclaredNetworkProperties.Length; i++) {
					PropertyData data = DeclaredNetworkProperties[i];
					stringBuilder.AppendLine($"\t\t{data.AccessModifierText} {data.FullyQualifiedTypeName} {data.Name} {GenerateGetterSetter(declaredPropertiesStartIndex + i, data)}");
				}

				string GenerateGetterSetter(int propertyIndex, PropertyData data) {

					return
			$@" {{ 
			get => {data.Name};
			set {{ 
				{NetworkEntityInterfaceFQN} iEntity = this;
				ref {NetworkPropertyInfoFQN} networkPropertyInfo = ref iEntity.NetworkProperties[{propertyIndex}];

				iEntity.LastDirtyTick = iEntity.SerializationContext?.Time ?? -1;
				networkPropertyInfo.Dirty = true;
				{data.Name} = value; 
			}}
		}}";

				}

				return stringBuilder.ToString();
			}

			private string GenerateInitializer() {
				StringBuilder stringBuilder = new StringBuilder();

				stringBuilder.AppendLine($"\t\tvoid {NetworkEntityInterfaceFQN}.Initialize() {{");

				stringBuilder.AppendLine($"\t\t\t{NetworkPropertyInfoFQN}[] networkProperties = new {NetworkPropertyInfoFQN}[{NetworkProperties.Length}];");

				stringBuilder.AppendLine($"\t\t\t{NetworkEntityInterfaceFQN} iEntity = this;");
				stringBuilder.AppendLine($"\t\t\tiEntity.NetworkProperties = networkProperties;");
				for (int i = 0; i < NetworkProperties.Length; i++) {
					PropertyData data = NetworkProperties[i];
					stringBuilder.AppendLine($"\t\t\tnetworkProperties[{i}] = new {NetworkPropertyInfoFQN} {{ Index = {i}, Name = \"{data.Name}\" }};");
				}

				stringBuilder.AppendLine($"\t\t}}");

				return stringBuilder.ToString();
			}

			private string GenerateSerialize() {
				StringBuilder stringBuilder = new StringBuilder();

				stringBuilder.AppendLine($"\t\tint {NetworkEntityInterfaceFQN}.Serialize({SerializationOptionsFQN} serializationOptions, {SpanFQN} buffer) {{");


				stringBuilder.AppendLine($"\t\t\treturn 0;");
				stringBuilder.AppendLine($"\t\t}}");

				return stringBuilder.ToString();
			}

			private string GenerateDeserialize() {
				StringBuilder stringBuilder = new StringBuilder();

				stringBuilder.AppendLine($"\t\tint {NetworkEntityInterfaceFQN}.Deserialize({SerializationOptionsFQN} serializationOptions, {ReadOnlySpanFQN} buffer) {{");


				stringBuilder.AppendLine($"\t\t\treturn 0;");
				stringBuilder.AppendLine($"\t\t}}");

				return stringBuilder.ToString();
			}

			private string GenerateClean() {
				StringBuilder stringBuilder = new StringBuilder();

				stringBuilder.AppendLine($"\t\tvoid {NetworkEntityInterfaceFQN}.Clean() {{");


				stringBuilder.AppendLine($"\t\t}}");

				return stringBuilder.ToString();
			}

		}


		public struct PropertyData {
			public byte AccessModifier { get; set; }
			public string AccessModifierText {
				get {
					switch (AccessModifier) {
						case 0: return "public";
						case 1: return "protected";
						case 2: return "private";
					}
					return "";
				}
			}

			public string FullyQualifiedTypeName { get; set; }
			public string Name { get; set; }

			public string InterfacePropertyDeclaration => $"{AccessModifierText} {FullyQualifiedTypeName} {Name} {{ get; set; }}";

		}

	}

}
