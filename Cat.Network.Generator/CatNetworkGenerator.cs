using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cat.Network.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class CatNetworkGenerator : IIncrementalGenerator {
	private const string NetworkEntityAttributeMetadataName = "Cat.Network.NetworkEntityAttribute";

	public void Initialize(IncrementalGeneratorInitializationContext context) {
		IncrementalValueProvider<ImmutableArray<NetworkEntityTypeModel>> networkEntityTypes = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				NetworkEntityAttributeMetadataName,
				static (node, _) => node is ClassDeclarationSyntax,
				static (syntaxContext, _) => NetworkEntityTypeModel.Create((INamedTypeSymbol)syntaxContext.TargetSymbol))
			.Collect();

		context.RegisterSourceOutput(networkEntityTypes, static (sourceProductionContext, models) => {
			foreach (NetworkEntityTypeModel model in models) NetworkEntityPartialClassPath.Generate(sourceProductionContext, model);
		});
	}
}
