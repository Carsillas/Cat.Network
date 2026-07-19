using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cat.Network.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class CatNetworkAnalyzer : DiagnosticAnalyzer {
	private const string NetworkObjectMetadataName = "Cat.Network.NetworkObject";
	private const string NetworkObjectAttributeMetadataName = "Cat.Network.NetworkObjectAttribute";
	private const string NetworkPropertyAttributeMetadataName = "Cat.Network.NetworkPropertyAttribute";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [
		..NetworkObjectAttributeAnalyzer.SupportedDiagnostics,
		..NetworkPropertyAttributeAnalyzer.SupportedDiagnostics
	];

	public override void Initialize(AnalysisContext context) {
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterCompilationStartAction(static compilationContext => {
			INamedTypeSymbol? networkObjectType = compilationContext.Compilation.GetTypeByMetadataName(NetworkObjectMetadataName);
			INamedTypeSymbol? networkObjectAttributeType = compilationContext.Compilation.GetTypeByMetadataName(NetworkObjectAttributeMetadataName);
			INamedTypeSymbol? networkPropertyAttributeType = compilationContext.Compilation.GetTypeByMetadataName(NetworkPropertyAttributeMetadataName);

			if (networkObjectType is null || networkObjectAttributeType is null || networkPropertyAttributeType is null) {
				return;
			}

			NetworkObjectAttributeAnalyzer.Register(compilationContext, networkObjectType, networkObjectAttributeType);
			NetworkPropertyAttributeAnalyzer.Register(compilationContext, networkObjectType, networkPropertyAttributeType);
		});
	}
}
