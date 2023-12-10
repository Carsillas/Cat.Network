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
						Rpcs = GetRpcsForSymbol(symbol).ToImmutableArray(),
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
			c.AddSource($"{source.Namespace}.{source.MetadataName}.RPCs", NetworkEntityRpcGenerator.GenerateRpcSource(source)));
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
					CompleteSerializationExpression = typeInfo.IsNetworkDataObject ?
						GetReferenceSerialization(propertySymbol.Name, propertySymbol.Symbol.Type, true) :
						GenerateTypeSerialization(propertySymbol.Name, propertySymbol.Symbol.Type),
					DeserializationExpression = typeInfo.IsNetworkDataObject ?
						GetReferenceDeserialization(propertySymbol.Name, propertySymbol.Symbol.Type) :
						GenerateTypeDeserialization(propertySymbol.Name, propertySymbol.Symbol.Type),
					PartialSerializationExpression = typeInfo.IsNetworkDataObject ?
						GetReferenceSerialization(propertySymbol.Name, propertySymbol.Symbol.Type, false) : null,
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
			.Select(propertySymbol => {
				ITypeSymbol itemType = ((INamedTypeSymbol)propertySymbol.Symbol.Type).TypeArguments[0];
				TypeInfo typeInfo = GetTypeInfo(itemType);
				return new NetworkCollectionData {
					Declared = propertySymbol.Declared,
					Name = propertySymbol.Name,
					CollectionTypeInfo = GetTypeInfo(propertySymbol.Symbol.Type),
					ItemTypeInfo = typeInfo,
					CompleteItemSerializationExpression = typeInfo.IsNetworkDataObject ?
						GetReferenceSerialization("item", itemType, true) :
						GenerateTypeSerialization("item", itemType),
					ItemDeserializationExpression = typeInfo.IsNetworkDataObject ?
						GetReferenceDeserialization("item", itemType) :
						GenerateTypeDeserialization("item", itemType),
					PartialItemSerializationExpression = typeInfo.IsNetworkDataObject ?
						GetReferenceSerialization("item", itemType, false) : null
				};
			});
		}

		private static IEnumerable<RpcMethodData> GetRpcsForSymbol(INamedTypeSymbol typeSymbol) {
			return GetExplicitSymbols<IMethodSymbol>(typeSymbol, RpcPrefix)
			.Select(methodSymbol => {

				var parameters = methodSymbol.Symbol.Parameters.Select(parameter => {
					TypeInfo typeInfo = GetTypeInfo(parameter.Type);

					RpcParameterAttribute? specialAttribute = null;

					foreach (var attributeData in parameter.GetAttributes()) {
						string attributeTypeFqn = attributeData.AttributeClass.ToDisplayString(FullyQualifiedFormat);

						if (attributeTypeFqn == ClientParameterAttributeFQN) {
							specialAttribute = RpcParameterAttribute.Client;
							break;
						}
						
						if (attributeTypeFqn == InstigatorParameterAttributeFQN) {
							specialAttribute = RpcParameterAttribute.Instigator;
							break;
						}
					}
					
					return new RpcParameterData {
						TypeInfo = typeInfo,
						SpecialAttribute = specialAttribute,
						ParameterName = parameter.Name,
						SerializationExpression = typeInfo.IsNetworkDataObject
							? GetReferenceSerialization(parameter.Name, parameter.Type, true)
							: GenerateTypeSerialization(parameter.Name, parameter.Type),
						DeserializationExpression = typeInfo.IsNetworkDataObject
							? GetReferenceDeserialization(parameter.Name, parameter.Type)
							: GenerateTypeDeserialization(parameter.Name, parameter.Type)
					};
				}).ToImmutableArray();

				string returnType = methodSymbol.Symbol.ReturnsVoid
					? "void"
					: methodSymbol.Symbol.ReturnType.ToDisplayString(FullyQualifiedFormat);

				ImmutableArray<RpcParameterData> classMethodParameters =
					parameters.Where(parameter => parameter.SpecialAttribute == null).ToImmutableArray();
				
				string interfaceInvocationParameters = string.Join(", ", parameters.Select(
					p => {
						if (p.SpecialAttribute == RpcParameterAttribute.Client) {
							return $"(({NetworkEntityInterfaceFQN})this).SerializationContext as {p.TypeInfo.FullyQualifiedTypeName}";
						}

						if (p.SpecialAttribute == RpcParameterAttribute.Instigator) {
							return "instigatorId";
						}

						return $"{p.ParameterName}";
				}));
				
				string interfaceMethodInvocation = $@"{methodSymbol.Name}({interfaceInvocationParameters})";
				string classMethodDeclaration = $"{returnType} {methodSymbol.Name}({string.Join(", ", classMethodParameters.Select(p => $"{p.TypeInfo.FullyQualifiedTypeName} {p.ParameterName}"))})";
				
				return new RpcMethodData {
					Declared = methodSymbol.Declared,
					ClassMethodDeclaration = classMethodDeclaration,
					InterfaceMethodDeclaration = methodSymbol.Symbol.ToDisplayString(InterfaceMethodDeclarationFormat),
					InterfaceMethodInvocation = interfaceMethodInvocation,
					InterfaceParameters = parameters,
					ClassParameters = classMethodParameters
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
