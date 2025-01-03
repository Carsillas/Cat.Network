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
				PassNodesOfType<RecordDeclarationSyntax>,
				(generatorSyntaxContext, cancellationToken) => {

					RecordDeclarationSyntax node = (RecordDeclarationSyntax)generatorSyntaxContext.Node;
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
			.Select(propertySymbol => {

				TypeInfo typeInfo = GetTypeInfo(propertySymbol.Symbol.Type);
				
				return new NetworkPropertyData {
					Declared = propertySymbol.Declared,
					Name = propertySymbol.Name,
					TypeInfo = typeInfo,
					CompleteSerializationExpression = typeInfo.IsNetworkDataObject ?
						GetReferenceSerialization(propertySymbol.Name, propertySymbol.Symbol.Type, true) :
						GenerateTypeSerialization(propertySymbol.Name, propertySymbol.Symbol.Type),
					PartialSerializationExpression = typeInfo.IsNetworkDataObject ?
						GetReferenceSerialization(propertySymbol.Name, propertySymbol.Symbol.Type, false) : null,
					DeserializationExpression = typeInfo.IsNetworkDataObject ?
						GetReferenceDeserialization(propertySymbol.Name, propertySymbol.Symbol.Type) :
						GenerateTypeDeserialization(propertySymbol.Name, propertySymbol.Symbol.Type),
					ExposeEvent = false,
					ForwardedAttributes = propertySymbol.Symbol.GetAttributes()
						.Where(attributeData => attributeData.AttributeClass.ToDisplayString(FullyQualifiedFormat) == ForwardedAttributeFQN )
						.Select(attributeData => (string)attributeData.ConstructorArguments.FirstOrDefault().Value)
						.ToImmutableArray()
				};
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