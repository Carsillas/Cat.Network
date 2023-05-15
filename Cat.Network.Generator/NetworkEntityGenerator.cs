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

using static Cat.Network.Generator.GeneratorUtils;
using static Cat.Network.Generator.Utils;

namespace Cat.Network.Generator {

	[Generator(LanguageNames.CSharp)]
	public partial class NetworkEntityGenerator : IIncrementalGenerator {


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

					var networkPropertiesCollection = ImmutableArray.CreateBuilder<NetworkPropertyData>();

					bool isNetworkEntity = false;
					INamedTypeSymbol currentSymbol = symbol;

					while (currentSymbol != null) {

						networkPropertiesCollection.AddRange(GetNetworkPropertiesForSymbol(currentSymbol));

						if (currentSymbol.ToDisplayString(FullyQualifiedFormat) == NetworkEntityFQN) {
							isNetworkEntity = true;
							break;
						}
						currentSymbol = currentSymbol.BaseType;
					}
					networkPropertiesCollection.Reverse();

					if (!isNetworkEntity) {
						return new NetworkEntityClassDefinition { IsNetworkEntity = false };
					}

					return new NetworkEntityClassDefinition {
						Name = symbol.ToDisplayString(TypeNameFormat),
						IsNetworkEntity = true,
						BaseTypeFQN = symbol.BaseType?.ToDisplayString(FullyQualifiedFormat),
						Namespace = symbol.ContainingNamespace.ToDisplayString(FullyQualifiedFormat),
						MetadataName = symbol.MetadataName,
						NetworkProperties = networkPropertiesCollection.ToImmutableArray(),
						DeclaredNetworkProperties = GetPropertyDatasForNode(generatorSyntaxContext, node).Reverse().ToImmutableArray(),
						RPCs = GetRPCsForSymbol(symbol).ToImmutableArray(),
						DeclaredRPCs = GetRPCsForSymbol(symbol).ToImmutableArray(),
					};

				}
			);


			IncrementalValuesProvider<NetworkEntityClassDefinition> allNetworkEntities =
				allClasses.Where(data => data.IsNetworkEntity);


			context.RegisterSourceOutput(allNetworkEntities, (c, source) => 
			c.AddSource($"{source.Namespace}.{source.MetadataName}.RPCs", source.GenerateRPCSource()));
			context.RegisterSourceOutput(allNetworkEntities, (c, source) => 
			c.AddSource($"{source.Namespace}.{source.MetadataName}.NetworkProperties", source.GenerateNetworkPropertySource()));
		}


		private static IEnumerable<NetworkPropertyData> GetNetworkPropertiesForSymbol(INamedTypeSymbol typeSymbol) {
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
			.Select(propertySymbol => new NetworkPropertyData {
				Name = propertySymbol.Name.Split('.').Last(),
				AccessModifier = 0,
				FullyQualifiedTypeName = propertySymbol.Type.ToDisplayString(FullyQualifiedFormat)
			});
		}
		private static IEnumerable<RPCMethodData> GetRPCsForSymbol(INamedTypeSymbol typeSymbol) {
			return typeSymbol
			.GetMembers()
			.OfType<IMethodSymbol>()
			.Where(s => {
				int lastDot = s.Name.LastIndexOf('.');
				if (lastDot > 0) {
					return s.Name.Substring(0, lastDot).Split('.').LastOrDefault() == RPCPrefix;
				}
				return false;
			})
			.OrderByDescending(propertySymbol => propertySymbol.Name)
			.Select(methodSymbol => new RPCMethodData {
				ClassMethodInvocation = methodSymbol.ToDisplayString(ClassMethodInvocationFormat),
				InterfaceMethodDeclaration = methodSymbol.ToDisplayString(InterfaceMethodDeclarationFormat),
				Parameters = methodSymbol.Parameters.Select(parameter => 
				new RPCParameterData { 
					FullyQualifiedTypeName = parameter.Type.ToDisplayString(FullyQualifiedFormat),
					ParameterName = parameter.Name 
				}).ToImmutableArray()
			});
		}

		private static IEnumerable<NetworkPropertyData> GetPropertyDatasForNode(GeneratorSyntaxContext generatorSyntaxContext, ClassDeclarationSyntax node) {
			return node.ChildNodes()
				.OfType<PropertyDeclarationSyntax>()
				.Where(propertySyntax => propertySyntax.ChildNodes().Any(child => child is ExplicitInterfaceSpecifierSyntax explicitSpecifier &&
					explicitSpecifier.DescendantNodes().OfType<IdentifierNameSyntax>().LastOrDefault()?.Identifier.Text == NetworkPropertyPrefix))
			.OrderByDescending(propertySyntax => propertySyntax.Identifier.Text)
			.Select(propertySyntax => new NetworkPropertyData {
				Name = propertySyntax.Identifier.Text,
				AccessModifier = 0,
				FullyQualifiedTypeName = generatorSyntaxContext.SemanticModel.GetDeclaredSymbol(propertySyntax).Type.ToDisplayString(FullyQualifiedFormat)
			});
		}

	}

}
