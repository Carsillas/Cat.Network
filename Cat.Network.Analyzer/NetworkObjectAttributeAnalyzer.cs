using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cat.Network.Analyzer;

internal static class NetworkObjectAttributeAnalyzer {
	private const string MissingNetworkObjectAttributeDiagnosticId = "CN0001";
	private const string InvalidNetworkObjectAttributeDiagnosticId = "CN0002";
	private const string NetworkObjectAttributeRequiresPartialDiagnosticId = "CN0003";

	private static readonly DiagnosticDescriptor MissingNetworkObjectAttributeRule = new(
		MissingNetworkObjectAttributeDiagnosticId,
		"NetworkObject-derived type must be marked with NetworkObjectAttribute",
		"Type '{0}' inherits NetworkObject but is not marked with NetworkObjectAttribute",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	private static readonly DiagnosticDescriptor InvalidNetworkObjectAttributeRule = new(
		InvalidNetworkObjectAttributeDiagnosticId,
		"NetworkObjectAttribute can only be used on NetworkObject-derived types",
		"Type '{0}' is marked with NetworkObjectAttribute but does not inherit NetworkObject",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	private static readonly DiagnosticDescriptor NetworkObjectAttributeRequiresPartialRule = new(
		NetworkObjectAttributeRequiresPartialDiagnosticId,
		"NetworkObjectAttribute requires a partial type",
		"Type '{0}' is marked with NetworkObjectAttribute but is not partial",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	public static ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [
		MissingNetworkObjectAttributeRule,
		InvalidNetworkObjectAttributeRule,
		NetworkObjectAttributeRequiresPartialRule
	];

	public static void Register(CompilationStartAnalysisContext context, INamedTypeSymbol networkObjectType, INamedTypeSymbol networkObjectAttributeType) {
		context.RegisterSymbolAction(
			symbolContext => Analyze(symbolContext, networkObjectType, networkObjectAttributeType),
			SymbolKind.NamedType);
	}

	private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol networkObjectType, INamedTypeSymbol networkObjectAttributeType) {
		INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;

		if (type.TypeKind != TypeKind.Class || SymbolEqualityComparer.Default.Equals(type, networkObjectType)) {
			return;
		}

		bool inheritsNetworkObject = NetworkAnalyzerHelpers.InheritsFrom(type, networkObjectType);
		bool hasNetworkObjectAttribute = NetworkAnalyzerHelpers.HasAttribute(type, networkObjectAttributeType);

		if (inheritsNetworkObject && !hasNetworkObjectAttribute) {
			context.ReportDiagnostic(Diagnostic.Create(
				MissingNetworkObjectAttributeRule,
				type.Locations.FirstOrDefault(),
				type.Name));
		}

		if (!inheritsNetworkObject && hasNetworkObjectAttribute) {
			context.ReportDiagnostic(Diagnostic.Create(
				InvalidNetworkObjectAttributeRule,
				type.Locations.FirstOrDefault(),
				type.Name));
		}

		if (inheritsNetworkObject && hasNetworkObjectAttribute && !NetworkAnalyzerHelpers.IsPartial(type, context.CancellationToken)) {
			context.ReportDiagnostic(Diagnostic.Create(
				NetworkObjectAttributeRequiresPartialRule,
				type.Locations.FirstOrDefault(),
				type.Name));
		}
	}
}
