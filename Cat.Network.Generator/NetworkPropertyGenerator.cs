using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Cat.Network.Generator {

	[Generator(LanguageNames.CSharp)]
	public class NetworkPropertyGenerator : IIncrementalGenerator {

		public const string NetworkPropertyAttributeMetadataName = "Cat.Network.Generator.NetworkPropertyAttribute";
		public const string ConditionalAttributeMetadataName = "Cat.Network.RPCs";
		public const string NetworkEntityMetadataName = "Cat.Network.Entities.NetworkEntity";

		private SymbolDisplayFormat FullyQualifiedFormat { get; } = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces, genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);
		private SymbolDisplayFormat TypeNameFormat { get; } = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly, genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);


		public void Initialize(IncrementalGeneratorInitializationContext context) {


			IncrementalValuesProvider<NetworkEntityClassDefinition> allClassesWithNetworkPropertyAttribute =
				context.SyntaxProvider.ForAttributeWithMetadataName(NetworkPropertyAttributeMetadataName,
				(syntaxNode, cancellationToken) => true,
				(generatorAttributeSyntaxContext, cancellationToken) => {

					INamedTypeSymbol symbol = (INamedTypeSymbol)generatorAttributeSyntaxContext.TargetSymbol;
					var node = (ClassDeclarationSyntax)generatorAttributeSyntaxContext.TargetNode;

					List<NetworkPropertyAttributeData> propertyAttributeDatas = new List<NetworkPropertyAttributeData>();
					List<NetworkPropertyAttributeData> declaredPropertyAttributeDatas = new List<NetworkPropertyAttributeData>();

					AppendNetworkPropertyAttributes(declaredPropertyAttributeDatas, symbol);

					bool symbolIsNetworkEntity = false;
					INamedTypeSymbol currentSymbol = symbol;
					while (currentSymbol != null) {
						AppendNetworkPropertyAttributes(propertyAttributeDatas, currentSymbol);
						if (currentSymbol.ToDisplayString(FullyQualifiedFormat) == NetworkEntityMetadataName) {
							symbolIsNetworkEntity = true;
							break;
						}

						currentSymbol = currentSymbol.BaseType;
					}

					propertyAttributeDatas.Reverse();
					declaredPropertyAttributeDatas.Reverse();

					return new NetworkEntityClassDefinition {
						Namespace = symbol.ContainingNamespace.ToDisplayString(FullyQualifiedFormat),
						ClassName = symbol.ToDisplayString(TypeNameFormat),
						MetadataName = symbol.MetadataName,
						NetworkPropertyAttributes = propertyAttributeDatas,
						DeclaredNetworkPropertyAttributes = declaredPropertyAttributeDatas,
						IsNetworkEntity = symbolIsNetworkEntity
					};

				});

			IncrementalValuesProvider<NetworkEntityClassDefinition> networkEntityClassesWithNetworkPropertyAttribute =
				allClassesWithNetworkPropertyAttribute.Where(data => data.IsNetworkEntity);

			context.RegisterSourceOutput(networkEntityClassesWithNetworkPropertyAttribute,
				(c, source) => c.AddSource($"{source.Namespace}.{source.MetadataName}.NetworkProperties", source.GenerateSource()));
		}

		private void AppendNetworkPropertyAttributes(List<NetworkPropertyAttributeData> propertyAttributeDatas, INamedTypeSymbol symbol) {
			propertyAttributeDatas.AddRange(symbol.GetAttributes()
				.Where(attribute => attribute.AttributeClass.ToDisplayString(FullyQualifiedFormat) == NetworkPropertyAttributeMetadataName)
				.Select(attribute => {
					return new NetworkPropertyAttributeData {
						AccessModifier = (byte)attribute.ConstructorArguments[0].Value,
						FullyQualifiedTypeName = (attribute.ConstructorArguments[1].Value as INamedTypeSymbol).ToDisplayString(FullyQualifiedFormat),
						Name = attribute.ConstructorArguments[2].Value.ToString()
					};
				})
				.OrderByDescending(data => data.Name));
		}

	}


	public struct NetworkEntityClassDefinition {

		public string Namespace { get; set; }
		public string ClassName { get; set; }
		public string MetadataName { get; set; }
		public string GenericTypeDefinition { get; set; }

		public bool IsNetworkEntity { get; set; }

		public List<NetworkPropertyAttributeData> NetworkPropertyAttributes { get; set; }
		public List<NetworkPropertyAttributeData> DeclaredNetworkPropertyAttributes { get; set; }


		private const string NetworkEntityInitializerInterfaceFQN = "Cat.Network.Generator.INetworkEntityInitializer";
		private const string NetworkPropertyFQN = "Cat.Network.Properties.NetworkProperty";
		public const string UnsafeFQN = "System.Runtime.CompilerServices.Unsafe";


		public string GenerateSource() {

			return $@"

namespace {Namespace} {{

	partial class {ClassName} : {NetworkEntityInitializerInterfaceFQN} {{

{GenerateProperties()}

{GenerateInitializer()}

	}}

}}

";

		}

		private string GenerateProperties() {

			StringBuilder stringBuilder = new StringBuilder();

			int declaredPropertiesStartIndex = NetworkPropertyAttributes.Count - DeclaredNetworkPropertyAttributes.Count;

			for (int i = 0; i < DeclaredNetworkPropertyAttributes.Count; i++) {
				NetworkPropertyAttributeData data = DeclaredNetworkPropertyAttributes[i];
				stringBuilder.AppendLine($"\t\t{data.AccessModifierText} {data.FullyQualifiedTypeName} {data.Name} {GenerateGetterSetter(declaredPropertiesStartIndex + i, data)}");
			}

			string GenerateGetterSetter(int propertyIndex, NetworkPropertyAttributeData data) {

				return 
		$@" {{ 
			get => {UnsafeFQN}.As<{NetworkPropertyFQN}<{data.FullyQualifiedTypeName}>>({UnsafeFQN}.As<{NetworkEntityInitializerInterfaceFQN}>(this).NetworkProperties[{propertyIndex}]).Value; 
			set => {UnsafeFQN}.As<{NetworkPropertyFQN}<{data.FullyQualifiedTypeName}>>({UnsafeFQN}.As<{NetworkEntityInitializerInterfaceFQN}>(this).NetworkProperties[{propertyIndex}]).Value = value; 
		}}";

			}

			return stringBuilder.ToString();
		}

		private string GenerateInitializer() {
			StringBuilder stringBuilder = new StringBuilder();

			stringBuilder.AppendLine($"\t\tvoid {NetworkEntityInitializerInterfaceFQN}.Initialize() {{");

			stringBuilder.AppendLine($"\t\t\t{NetworkPropertyFQN}[] networkProperties = new {NetworkPropertyFQN}[{NetworkPropertyAttributes.Count}];");

			stringBuilder.AppendLine($"\t\t\t{NetworkEntityInitializerInterfaceFQN} entityInitializer = this;");
			stringBuilder.AppendLine($"\t\t\tentityInitializer.NetworkProperties = networkProperties;");
			for (int i = 0; i < NetworkPropertyAttributes.Count; i++) {
				NetworkPropertyAttributeData data = NetworkPropertyAttributes[i];
				stringBuilder.AppendLine($"\t\t\tnetworkProperties[{i}] = new {GetNetworkPropertyClass(data)} {{ Index = {i}, Name = \"{data.Name}\" }};");
			}

			stringBuilder.AppendLine($"\t\t}}");

			return stringBuilder.ToString();
		}
	
		private string GetNetworkPropertyClass(NetworkPropertyAttributeData data) {
			string PropertyNamespace = "Cat.Network.Properties";
			string PropertyType = data.FullyQualifiedTypeName switch {
				"System.Int32" => "Int32NetworkProperty",
				"System.Boolean" => "BooleanNetworkProperty",
				_ => "Error!"
			};
			return $"{PropertyNamespace}.{PropertyType}";
		}

	}

	public struct NetworkPropertyAttributeData {

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

	}


}
