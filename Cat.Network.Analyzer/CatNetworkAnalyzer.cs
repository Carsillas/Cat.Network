using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cat.Network.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class CatNetworkAnalyzer : DiagnosticAnalyzer {
	private const string NetworkObjectMetadataName = "Cat.Network.NetworkObject";
	private const string NetworkObjectAttributeMetadataName = "Cat.Network.NetworkObjectAttribute";
	private const string NetworkPropertyAttributeMetadataName = "Cat.Network.NetworkPropertyAttribute";
	private const string NetworkCollectionAttributeMetadataName = "Cat.Network.NetworkCollectionAttribute";
	private const string UpgradeToAttributeMetadataName = "Cat.Network.UpgradeToAttribute";
	private const string NetworkObjectUpgradeReaderMetadataName = "Cat.Network.NetworkObjectUpgradeReader";
	private const string NetworkObjectUpgradeWriterMetadataName = "Cat.Network.NetworkObjectUpgradeWriter";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [
		..NetworkObjectAttributeAnalyzer.SupportedDiagnostics,
		..NetworkPropertyAttributeAnalyzer.SupportedDiagnostics,
		..NetworkCollectionAttributeAnalyzer.SupportedDiagnostics,
		..UpgradeToAttributeAnalyzer.SupportedDiagnostics
	];

	public override void Initialize(AnalysisContext context) {
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterCompilationStartAction(static compilationContext => {
			INamedTypeSymbol? networkObjectType = compilationContext.Compilation.GetTypeByMetadataName(NetworkObjectMetadataName);
			INamedTypeSymbol? networkObjectAttributeType = compilationContext.Compilation.GetTypeByMetadataName(NetworkObjectAttributeMetadataName);
			INamedTypeSymbol? networkPropertyAttributeType = compilationContext.Compilation.GetTypeByMetadataName(NetworkPropertyAttributeMetadataName);
			INamedTypeSymbol? networkCollectionAttributeType = compilationContext.Compilation.GetTypeByMetadataName(NetworkCollectionAttributeMetadataName);
			INamedTypeSymbol? upgradeToAttributeType = compilationContext.Compilation.GetTypeByMetadataName(UpgradeToAttributeMetadataName);
			INamedTypeSymbol? upgradeReaderType = compilationContext.Compilation.GetTypeByMetadataName(NetworkObjectUpgradeReaderMetadataName);
			INamedTypeSymbol? upgradeWriterType = compilationContext.Compilation.GetTypeByMetadataName(NetworkObjectUpgradeWriterMetadataName);

			if (networkObjectType is null || networkObjectAttributeType is null || networkPropertyAttributeType is null || networkCollectionAttributeType is null) {
				return;
			}

			NetworkObjectAttributeAnalyzer.Register(compilationContext, networkObjectType, networkObjectAttributeType);
			NetworkPropertyAttributeAnalyzer.Register(compilationContext, networkObjectType, networkPropertyAttributeType);
			NetworkCollectionAttributeAnalyzer.Register(compilationContext, networkObjectType, networkCollectionAttributeType);
			if (upgradeToAttributeType is not null && upgradeReaderType is not null && upgradeWriterType is not null) {
				UpgradeToAttributeAnalyzer.Register(compilationContext, networkObjectType, networkObjectAttributeType, upgradeToAttributeType, upgradeReaderType, upgradeWriterType);
			}
		});
	}
}
