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


		private static SymbolDisplayFormat FullyQualifiedFormat { get; } = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces, genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);
		private static SymbolDisplayFormat TypeNameFormat { get; } = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly, genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);
		private static SymbolDisplayFormat InterfaceMethodDeclarationFormat { get; } = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces, parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeDefaultValue | SymbolDisplayParameterOptions.IncludeName, memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType);
		private static SymbolDisplayFormat ClassMethodInvocationFormat { get; } = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces, parameterOptions: SymbolDisplayParameterOptions.IncludeName, memberOptions: SymbolDisplayMemberOptions.IncludeParameters);


		public void Initialize(IncrementalGeneratorInitializationContext context) {

			IncrementalValuesProvider<NetworkEntityClassDefinition> allClasses = context.SyntaxProvider.CreateSyntaxProvider(
				PassNodesOfType<ClassDeclarationSyntax>,
				(generatorSyntaxContext, cancellationToken) => {

					ClassDeclarationSyntax node = (ClassDeclarationSyntax)generatorSyntaxContext.Node;
					INamedTypeSymbol symbol = generatorSyntaxContext.SemanticModel.GetDeclaredSymbol(node);

					if (!IsNetworkEntityType(symbol)) {
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


			context.RegisterSourceOutput(allNetworkEntities, (c, source) =>
			c.AddSource($"{source.Namespace}.{source.MetadataName}.RPCs", NetworkEntityPropertyGenerator.GenerateNetworkPropertySource(source)));
			context.RegisterSourceOutput(allNetworkEntities, (c, source) =>
			c.AddSource($"{source.Namespace}.{source.MetadataName}.NetworkProperties", NetworkEntityRPCGenerator.GenerateRPCSource(source)));
			context.RegisterSourceOutput(allNetworkEntities, (c, source) =>
			c.AddSource($"{source.Namespace}.{source.MetadataName}.NetworkCollections", NetworkEntityCollectionGenerator.GenerateNetworkCollectionSource(source)));
			context.RegisterSourceOutput(allNetworkEntities, (c, source) =>
			c.AddSource($"{source.Namespace}.{source.MetadataName}.Interface", NetworkEntityInterfaceImplementationGenerator.GenerateNetworkEntitySource(source)));
		}


		private static bool IsNetworkEntityType(INamedTypeSymbol typeSymbol) {
			INamedTypeSymbol currentSymbol = typeSymbol;

			while (currentSymbol != null) {
				if (currentSymbol.ToDisplayString(FullyQualifiedFormat) == NetworkEntityFQN) {
					return true;
				}
				currentSymbol = currentSymbol.BaseType;
			}

			return false;
		}


		private static IEnumerable<NetworkPropertyData> GetNetworkPropertiesForSymbol(INamedTypeSymbol typeSymbol) {
			return GetExplicitSymbols<IPropertySymbol>(typeSymbol, NetworkPropertyPrefix)
			.Select(propertySymbol => new NetworkPropertyData {
				Declared = propertySymbol.Declared,
				Name = propertySymbol.Name,
				AccessModifier = 0,
				FullyQualifiedTypeName = propertySymbol.Symbol.Type.ToDisplayString(FullyQualifiedFormat)
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
				FullyQualifiedTypeName = propertySymbol.Symbol.Type.ToDisplayString(FullyQualifiedFormat),
				Item1FullyQualifiedTypeName = ((INamedTypeSymbol)propertySymbol.Symbol.Type).TypeArguments[0].ToDisplayString(FullyQualifiedFormat)
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
					FullyQualifiedTypeName = parameter.Type.ToDisplayString(FullyQualifiedFormat),
					ParameterName = parameter.Name
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
