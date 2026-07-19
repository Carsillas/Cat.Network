using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cat.Network.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CatNetworkAnalyzer : DiagnosticAnalyzer {
	private const string NetworkEntityMetadataName = "Cat.Network.NetworkEntity";
	private const string NetworkEntityAttributeMetadataName = "Cat.Network.NetworkEntityAttribute";
	private const string NetworkPropertyAttributeMetadataName = "Cat.Network.NetworkPropertyAttribute";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [
		..NetworkEntityAttributeAnalyzer.SupportedDiagnostics,
		..NetworkPropertyAttributeAnalyzer.SupportedDiagnostics
	];

	public override void Initialize(AnalysisContext context) {
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterCompilationStartAction(static compilationContext => {
			INamedTypeSymbol? networkEntityType = compilationContext.Compilation.GetTypeByMetadataName(NetworkEntityMetadataName);
			INamedTypeSymbol? networkEntityAttributeType = compilationContext.Compilation.GetTypeByMetadataName(NetworkEntityAttributeMetadataName);
			INamedTypeSymbol? networkPropertyAttributeType = compilationContext.Compilation.GetTypeByMetadataName(NetworkPropertyAttributeMetadataName);

			if (networkEntityType is null || networkEntityAttributeType is null || networkPropertyAttributeType is null) {
				return;
			}

			NetworkEntityAttributeAnalyzer.Register(compilationContext, networkEntityType, networkEntityAttributeType);
			NetworkPropertyAttributeAnalyzer.Register(compilationContext, networkEntityType, networkPropertyAttributeType);
		});
	}
}
