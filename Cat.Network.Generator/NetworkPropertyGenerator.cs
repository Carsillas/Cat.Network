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

		private static SymbolDisplayFormat FullyQualifiedFormat { get; } = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces, genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);
		private static SymbolDisplayFormat TypeNameFormat { get; } = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly, genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);


		public void Initialize(IncrementalGeneratorInitializationContext context) {


			IncrementalValuesProvider<NetworkEntityClassDefinition> allNetworkEntities = context.SyntaxProvider.CreateSyntaxProvider(
				PassNodesOfType<ClassDeclarationSyntax>(),
				(generatorSyntaxContext, cancellationToken) => {

					ClassDeclarationSyntax node = (ClassDeclarationSyntax)generatorSyntaxContext.Node;
					INamedTypeSymbol symbol = generatorSyntaxContext.SemanticModel.GetDeclaredSymbol(node);

					bool isNetworkEntity = false;
					INamedTypeSymbol currentSymbol = symbol.BaseType;

					var builder = ImmutableArray.CreateBuilder<PropertyData>();

					builder.AddRange(GetPropertyDatasForSymbol(symbol));

					while (currentSymbol != null) {
						builder.AddRange(GetPropertyDatasForSymbol(currentSymbol));

						if (currentSymbol.ToDisplayString(FullyQualifiedFormat) == NetworkEntityMetadataName) {
							isNetworkEntity = true;
							break;
						}
						currentSymbol = currentSymbol.BaseType;
					}

					return new NetworkEntityClassDefinition {
						Name = symbol.ToDisplayString(TypeNameFormat),
						IsNetworkEntity = isNetworkEntity,
						Namespace = symbol.ContainingNamespace.ToDisplayString(FullyQualifiedFormat),
						MetadataName = symbol.MetadataName,
						NetworkProperties = builder.Reverse().ToImmutableArray(),
						DeclaredNetworkProperties = GetPropertyDatasForSymbol(symbol).Reverse().ToImmutableArray()
					};

					static IEnumerable<PropertyData> GetPropertyDatasForSymbol(INamedTypeSymbol typeSymbol) {
						return typeSymbol.GetMembers().OfType<IPropertySymbol>()
						.Where(propertySymbol => propertySymbol.ExplicitInterfaceImplementations
							.Any(explicitImplementation => SymbolEqualityComparer.Default.Equals(explicitImplementation.ContainingType.ContainingType, typeSymbol)
								&& explicitImplementation.ContainingType.Name == NetworkPropertyPrefix))
						.OrderByDescending(propertySymbol => propertySymbol.Name)
						.Select(propertySymbol => new PropertyData {
							Name = propertySymbol.Name.Split('.').Last(),
							AccessModifier = 0,
							FullyQualifiedTypeName = propertySymbol.Type.ToDisplayString(FullyQualifiedFormat)
						});
					}
				}
			);

			IncrementalValuesProvider<PropertyDeclaration> allNetworkProperties =
				context.SyntaxProvider.CreateSyntaxProvider(
					And(PassNodesOfType<PropertyDeclarationSyntax>(), PassNodesWithExplicitInterfaceSpecifier(NetworkPropertyPrefix)),
					(generatorSyntaxContext, cancellationToken) => {

						PropertyDeclarationSyntax node = (PropertyDeclarationSyntax)generatorSyntaxContext.Node;
						IPropertySymbol symbol = generatorSyntaxContext.SemanticModel.GetDeclaredSymbol(node);
						INamedTypeSymbol typeSymbol = symbol.ContainingType;

						bool typeSymbolIsNetworkEntity = false;
						INamedTypeSymbol currentSymbol = typeSymbol;

						var builder = ImmutableArray.CreateBuilder<string>();

						while (currentSymbol != null) {
							builder.Add(currentSymbol.MetadataName);
							if (currentSymbol.ToDisplayString(FullyQualifiedFormat) == NetworkEntityMetadataName) {
								typeSymbolIsNetworkEntity = true;
								break;
							}
							currentSymbol = currentSymbol.BaseType;
						}

						return new PropertyDeclaration {
							ClassNamespace = typeSymbol.ContainingNamespace.ToDisplayString(FullyQualifiedFormat),
							ClassName = typeSymbol.ToDisplayString(TypeNameFormat),
							ClassMetadataName = typeSymbol.MetadataName,
							IsNetworkEntityProperty = typeSymbolIsNetworkEntity,
							PropertyData = new PropertyData {
								Name = symbol.Name.Split('.').Last(),
								AccessModifier = 0,
								FullyQualifiedTypeName = symbol.Type.ToDisplayString(FullyQualifiedFormat)
							},
							InheritanceChain = builder.ToImmutable()
						};
					}
				);

			IncrementalValuesProvider<PropertyDeclaration> allNetworkEntityNetworkProperties =
				allNetworkProperties.Where(data => data.IsNetworkEntityProperty);

			var collected = allNetworkEntityNetworkProperties.Collect();

			var networkEntityClassDefinition = collected.SelectMany((collection, token) => {

				var grouped = collection.GroupBy(propertyDeclaration => $"{propertyDeclaration.ClassNamespace}.{propertyDeclaration.ClassMetadataName}");

				Dictionary<string, ImmutableArray<PropertyData>> declaredPropertiesLookup = grouped.ToDictionary(
					grouping => grouping.First().ClassMetadataName,
					grouping => grouping.Select(propertyDeclaration => propertyDeclaration.PropertyData).OrderBy(propertyData => propertyData.Name).ToImmutableArray());

				return grouped.Select(grouping => new NetworkEntityClassDefinition {
					Namespace = grouping.First().ClassNamespace,
					Name = grouping.First().ClassName,
					MetadataName = grouping.First().ClassMetadataName,
					NetworkProperties = grouping.First().InheritanceChain.Reverse().SelectMany(metadataName => {
						if (declaredPropertiesLookup.ContainsKey(metadataName)) {
							return declaredPropertiesLookup[metadataName];
						} else {
							return new[] { new PropertyData { Name = metadataName + $"({string.Join(",", declaredPropertiesLookup.Keys)})", FullyQualifiedTypeName = "ahhh" } }.ToImmutableArray();
						}
					}).ToImmutableArray(),
					DeclaredNetworkProperties = grouping.Select(propertyDeclaration => propertyDeclaration.PropertyData).ToImmutableArray()
				});
			});


			context.RegisterSourceOutput(networkEntityClassDefinition,
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
			private const string NetworkPropertyFQN = "Cat.Network.Properties.NetworkProperty";
			public const string UnsafeFQN = "System.Runtime.CompilerServices.Unsafe";


			public string GenerateSource() {

				return $@"

namespace {Namespace} {{

	partial class {Name} : {NetworkEntityInterfaceFQN}, {Name}.{NetworkPropertyPrefix} {{

{GenerateInterface()}
{GenerateProperties()}
{GenerateInitializer()}

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
			get => {UnsafeFQN}.As<{NetworkPropertyFQN}<{data.FullyQualifiedTypeName}>>({UnsafeFQN}.As<{NetworkEntityInterfaceFQN}>(this).NetworkProperties[{propertyIndex}]).Value; 
			set => {UnsafeFQN}.As<{NetworkPropertyFQN}<{data.FullyQualifiedTypeName}>>({UnsafeFQN}.As<{NetworkEntityInterfaceFQN}>(this).NetworkProperties[{propertyIndex}]).Value = value; 
		}}";

				}

				return stringBuilder.ToString();
			}

			private string GenerateInitializer() {
				StringBuilder stringBuilder = new StringBuilder();

				stringBuilder.AppendLine($"\t\tvoid {NetworkEntityInterfaceFQN}.Initialize() {{");

				stringBuilder.AppendLine($"\t\t\t{NetworkPropertyFQN}[] networkProperties = new {NetworkPropertyFQN}[{NetworkProperties.Length}];");

				stringBuilder.AppendLine($"\t\t\t{NetworkEntityInterfaceFQN} iEntity = this;");
				stringBuilder.AppendLine($"\t\t\tiEntity.NetworkProperties = networkProperties;");
				for (int i = 0; i < NetworkProperties.Length; i++) {
					PropertyData data = NetworkProperties[i];
					stringBuilder.AppendLine($"\t\t\tnetworkProperties[{i}] = new {GetNetworkPropertyClass(data)} {{ Entity = this, Index = {i}, Name = \"{data.Name}\" }};");
				}

				stringBuilder.AppendLine($"\t\t}}");

				return stringBuilder.ToString();
			}

			private string GetNetworkPropertyClass(PropertyData data) {
				string PropertyNamespace = "Cat.Network.Properties";
				string PropertyType = data.FullyQualifiedTypeName switch {
					"System.Int32" => "Int32NetworkProperty",
					"System.Boolean" => "BooleanNetworkProperty",
					_ => "Error!"
				};
				return $"{PropertyNamespace}.{PropertyType}";
			}

		}

		public struct PropertyDeclaration {
			public string ClassNamespace { get; set; }
			public string ClassName { get; set; }
			public string ClassMetadataName { get; set; }
			public bool IsNetworkEntityProperty { get; set; }
			public PropertyData PropertyData { get; set; }

			public ImmutableArray<string> InheritanceChain { get; set; }

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
