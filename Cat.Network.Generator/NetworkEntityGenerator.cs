using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

using static Cat.Network.Generator.Utils;

namespace Cat.Network.Generator {

	[Generator(LanguageNames.CSharp)]
	public class NetworkEntityGenerator : IIncrementalGenerator {

		public void Initialize(IncrementalGeneratorInitializationContext context) {

			IncrementalValuesProvider<NetworkEntityClassDefinition> allClasses = context.SyntaxProvider.CreateSyntaxProvider(
				PassNodesOfType<ClassDeclarationSyntax>,
				(generatorSyntaxContext, cancellationToken) => {

					ClassDeclarationSyntax node = (ClassDeclarationSyntax)generatorSyntaxContext.Node;
					INamedTypeSymbol symbol = generatorSyntaxContext.SemanticModel.GetDeclaredSymbol(node);

					if (!IsTypeWithFQN(symbol, NetworkEntityFQN)) {
						return new NetworkEntityClassDefinition { IsNetworkEntity = false };
					}

					return new NetworkEntityClassDefinition {
						Name = symbol.ToDisplayString(TypeNameFormat),
						IsNetworkEntity = true,
						BaseTypeFQN = symbol.BaseType?.ToDisplayString(FullyQualifiedFormat),
						Namespace = symbol.ContainingNamespace.ToDisplayString(FullyQualifiedFormat),
						MetadataName = symbol.MetadataName,
						NetworkProperties = GetNetworkPropertiesForSymbol(symbol).Reverse().ToImmutableArray(),
						NetworkCollections = GetNetworkCollectionsForSymbol(symbol).Reverse().ToImmutableArray(),
						RPCs = GetRPCsForSymbol(symbol).ToImmutableArray(),
					};

				}
			);


			IncrementalValuesProvider<NetworkEntityClassDefinition> allNetworkEntities =
				allClasses.Where(data => data.IsNetworkEntity);


			NetworkEntityPropertyGenerator propertyGenerator = new NetworkEntityPropertyGenerator();
			
			NetworkEntityInterfaceImplementationGenerator interfaceGenerator =
				new NetworkEntityInterfaceImplementationGenerator();
			
			context.RegisterSourceOutput(allNetworkEntities, (c, source) =>
			c.AddSource($"{source.Namespace}.{source.MetadataName}.NetworkProperties", propertyGenerator.GenerateNetworkPropertySource(source)));
			context.RegisterSourceOutput(allNetworkEntities, (c, source) =>
			c.AddSource($"{source.Namespace}.{source.MetadataName}.RPCs", NetworkEntityRPCGenerator.GenerateRPCSource(source)));
			context.RegisterSourceOutput(allNetworkEntities, (c, source) =>
			c.AddSource($"{source.Namespace}.{source.MetadataName}.NetworkCollections", NetworkEntityCollectionGenerator.GenerateNetworkCollectionSource(source)));
			context.RegisterSourceOutput(allNetworkEntities, (c, source) =>
			c.AddSource($"{source.Namespace}.{source.MetadataName}.Interface", interfaceGenerator.GenerateNetworkSerializableSource(source)));
		}
		
		private static IEnumerable<NetworkPropertyData> GetNetworkPropertiesForSymbol(INamedTypeSymbol typeSymbol) {
			return GetExplicitSymbols<IPropertySymbol>(typeSymbol, NetworkPropertyPrefix)
			.Select(propertySymbol => {
				
				TypeInfo typeInfo = GetTypeInfo(propertySymbol.Symbol.Type);
				
				return new NetworkPropertyData {
					Declared = propertySymbol.Declared,
					Name = propertySymbol.Name,
					TypeInfo = typeInfo,
					SerializationExpression = typeInfo.IsNetworkDataObject ?
						GetReferenceSerialization(propertySymbol.Symbol.Type, propertySymbol.Name) :
						GenerateTypeSerialization(propertySymbol.Name, propertySymbol.Symbol.Type),
					DeserializationExpression = typeInfo.IsNetworkDataObject ?
						GetReferenceDeserialization(propertySymbol.Symbol.Type, propertySymbol.Name) :
						GenerateTypeDeserialization(propertySymbol.Name, propertySymbol.Symbol.Type),
					ExposeEvent = propertySymbol.Symbol.GetAttributes().Any(attributeData =>
						attributeData.AttributeClass.ToDisplayString(FullyQualifiedFormat) ==
						NetworkPropertyChangedEventAttributeFQN)
				};
			});
		}

		private static IEnumerable<NetworkCollectionData> GetNetworkCollectionsForSymbol(INamedTypeSymbol typeSymbol) {
			return GetExplicitSymbols<IPropertySymbol>(typeSymbol, NetworkCollectionPrefix)
			.Where(propertySymbol =>
				propertySymbol.Symbol.Type is INamedTypeSymbol namedTypeSymbol &&
				namedTypeSymbol.IsGenericType &&
				namedTypeSymbol.TypeArguments.Length == 1 &&
				namedTypeSymbol.TypeArguments[0] is INamedTypeSymbol)
			.Select(propertySymbol => new NetworkCollectionData {
				Declared = propertySymbol.Declared,
				Name = propertySymbol.Name,
				CollectionTypeInfo = GetTypeInfo(propertySymbol.Symbol.Type),
				ItemTypeInfo = GetTypeInfo(((INamedTypeSymbol)propertySymbol.Symbol.Type).TypeArguments[0]),
				ItemSerializationExpression = GenerateTypeSerialization("item", ((INamedTypeSymbol)propertySymbol.Symbol.Type).TypeArguments[0]),
				ItemDeserializationExpression = GenerateTypeDeserialization("item", ((INamedTypeSymbol)propertySymbol.Symbol.Type).TypeArguments[0])
			});
		}

		private static IEnumerable<RPCMethodData> GetRPCsForSymbol(INamedTypeSymbol typeSymbol) {
			return GetExplicitSymbols<IMethodSymbol>(typeSymbol, RPCPrefix)
			.Select(methodSymbol => new RPCMethodData {
				Declared = methodSymbol.Declared,
				ClassMethodInvocation = methodSymbol.Symbol.ToDisplayString(ClassMethodInvocationFormat),
				InterfaceMethodDeclaration = methodSymbol.Symbol.ToDisplayString(InterfaceMethodDeclarationFormat),
				Parameters = methodSymbol.Symbol.Parameters.Select(parameter =>
				new RPCParameterData {
					TypeInfo = GetTypeInfo(parameter.Type),
					ParameterName = parameter.Name,
					SerializationExpression = GenerateTypeSerialization(parameter.Name, parameter.Type),
					DeserializationExpression = GenerateTypeDeserialization(parameter.Name, parameter.Type)
				}).ToImmutableArray()
			});
		}

		private static IEnumerable<ExplicitSymbol<T>> GetExplicitSymbols<T>(INamedTypeSymbol typeSymbol, string explicitInterface) where T : ISymbol {

			INamedTypeSymbol currentSymbol = typeSymbol;

			while (currentSymbol != null) {
				IEnumerable<ExplicitSymbol<T>> symbols = currentSymbol
					.GetMembers()
					.OfType<T>()
					.Where(s => {
						int lastDot = s.Name.LastIndexOf('.');
						if (lastDot > 0) {
							return s.Name.Substring(0, lastDot).Split('.').LastOrDefault() == explicitInterface;
						}
						return false;
					})
					.OrderByDescending(symbol => symbol.Name)
					.Select(symbol => new ExplicitSymbol<T> {
						Declared = ReferenceEquals(currentSymbol, typeSymbol), // explicitly using reference equality
						Symbol = symbol
					});

				foreach (var symbol in symbols) {
					yield return symbol;
				}

				currentSymbol = currentSymbol.BaseType;
			}
		}

		private struct ExplicitSymbol<T> where T : ISymbol {
			public bool Declared { get; set; }
			public string Name => Symbol.Name.Split('.').Last();
			public T Symbol { get; set; }
		}


	}

}
