using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Cat.Network.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class CatNetworkGenerator : IIncrementalGenerator {
	private const string NetworkObjectAttributeMetadataName = "Cat.Network.NetworkObjectAttribute";

	public void Initialize(IncrementalGeneratorInitializationContext context) {
		IncrementalValueProvider<ImmutableArray<(NetworkObjectTypeModel? Model, ImmutableArray<Diagnostic> Diagnostics)>> networkObjectTypes = context.SyntaxProvider
			.ForAttributeWithMetadataName(
				NetworkObjectAttributeMetadataName,
				static (node, _) => node is ClassDeclarationSyntax,
				static (syntaxContext, _) => {
					INamedTypeSymbol type = (INamedTypeSymbol)syntaxContext.TargetSymbol;
					ImmutableArray<Diagnostic> diagnostics = NetworkTypeValidation.GetDiagnostics(type, syntaxContext.SemanticModel.Compilation);
					return (Model: diagnostics.IsEmpty ? NetworkObjectTypeModel.Create(type) : null, Diagnostics: diagnostics);
				})
			.Collect();

		context.RegisterSourceOutput(networkObjectTypes, static (sourceProductionContext, models) => {
			foreach ((NetworkObjectTypeModel? model, ImmutableArray<Diagnostic> diagnostics) in models) {
				foreach (Diagnostic diagnostic in diagnostics) sourceProductionContext.ReportDiagnostic(diagnostic);
				if (model is null) continue;
				NetworkObjectPropertiesGenerator.Generate(sourceProductionContext, model);
				NetworkObjectMessagesGenerator.Generate(sourceProductionContext, model);
				if (!model.IsAbstract) {
					NetworkObjectSerializerGenerator.Generate(sourceProductionContext, model);
				}
			}
		});
	}
}
