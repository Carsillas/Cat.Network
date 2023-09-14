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
	public class NetworkDataObjectGenerator : IIncrementalGenerator {

		public void Initialize(IncrementalGeneratorInitializationContext context) {

			IncrementalValuesProvider<NetworkDataObjectDefinition> allClasses = context.SyntaxProvider.CreateSyntaxProvider(
				PassNodesOfType<ClassDeclarationSyntax>,
				(generatorSyntaxContext, cancellationToken) => {

					ClassDeclarationSyntax node = (ClassDeclarationSyntax)generatorSyntaxContext.Node;
					INamedTypeSymbol symbol = generatorSyntaxContext.SemanticModel.GetDeclaredSymbol(node);

					if (!IsTypeWithFQN(symbol, NetworkDataObjectFQN)) {
						return new NetworkDataObjectDefinition { IsNetworkDataObject = false };
					}

					return new NetworkDataObjectDefinition {
						Name = symbol.ToDisplayString(TypeNameFormat),
						IsNetworkDataObject = true,
						BaseTypeFQN = symbol.BaseType?.ToDisplayString(FullyQualifiedFormat),
						Namespace = symbol.ContainingNamespace.ToDisplayString(FullyQualifiedFormat),
						MetadataName = symbol.MetadataName,
						NetworkProperties = GetNetworkPropertiesForSymbol(symbol).Reverse().ToImmutableArray()
					};

				}
			);


			IncrementalValuesProvider<NetworkDataObjectDefinition> allNetworkEntities =
				allClasses.Where(data => data.IsNetworkDataObject);
			
			NetworkDataObjectPropertyGenerator propertyGenerator = new NetworkDataObjectPropertyGenerator();

			NetworkDataObjectInterfaceImplementationGenerator networkDataObjectInterfaceImplementationGenerator =
				new NetworkDataObjectInterfaceImplementationGenerator();

			context.RegisterSourceOutput(allNetworkEntities, (c, source) =>
			c.AddSource($"{source.Namespace}.{source.MetadataName}.NetworkProperties", propertyGenerator.GenerateNetworkPropertySource(source)));
			context.RegisterSourceOutput(allNetworkEntities, (c, source) =>
			c.AddSource($"{source.Namespace}.{source.MetadataName}.Interface", networkDataObjectInterfaceImplementationGenerator.GenerateNetworkSerializableSource(source)));
		}

		private static IEnumerable<NetworkPropertyData> GetNetworkPropertiesForSymbol(INamedTypeSymbol typeSymbol) {
			return GetExplicitSymbols<IPropertySymbol>(typeSymbol, NetworkPropertyPrefix)
			.Select(propertySymbol => new NetworkPropertyData {
				Declared = propertySymbol.Declared,
				Name = propertySymbol.Name,
				TypeInfo = GetTypeInfo(propertySymbol.Symbol.Type),
				SerializationExpression = GenerateTypeSerialization(propertySymbol.Name, propertySymbol.Symbol.Type),
				DeserializationExpression = GenerateTypeDeserialization(propertySymbol.Name, propertySymbol.Symbol.Type),
				ExposeEvent = propertySymbol.Symbol.GetAttributes().Any(attributeData => attributeData.AttributeClass.ToDisplayString(FullyQualifiedFormat) == NetworkPropertyChangedEventAttributeFQN)
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