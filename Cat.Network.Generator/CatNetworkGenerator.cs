using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cat.Network.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class CatNetworkGenerator : IIncrementalGenerator {
	private const string NetworkObjectAttributeMetadataName = "Cat.Network.NetworkObjectAttribute";

	public void Initialize(IncrementalGeneratorInitializationContext context) {
		IncrementalValueProvider<ImmutableArray<NetworkObjectTypeModel>> networkObjectTypes = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				NetworkObjectAttributeMetadataName,
				static (node, _) => node is ClassDeclarationSyntax,
				static (syntaxContext, _) => NetworkObjectTypeModel.Create(
					(INamedTypeSymbol)syntaxContext.TargetSymbol,
					syntaxContext.SemanticModel.Compilation.GetTypeByMetadataName("Cat.Network.NetworkObject")))
			.Collect();

		context.RegisterSourceOutput(networkObjectTypes, static (sourceProductionContext, models) => {
			foreach (NetworkObjectTypeModel model in models) {
				NetworkObjectPropertiesGenerator.Generate(sourceProductionContext, model);
				NetworkObjectMessagesGenerator.Generate(sourceProductionContext, model);
				if (!model.IsAbstract) {
					NetworkObjectSerializerGenerator.Generate(sourceProductionContext, model);
				}
			}
		});
	}
}
