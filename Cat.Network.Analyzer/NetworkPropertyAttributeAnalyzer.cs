using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cat.Network.Analyzer;

internal static class NetworkPropertyAttributeAnalyzer {
	private const string InvalidNetworkPropertyAttributeDiagnosticId = "CN0004";
	private const string NetworkPropertyAttributeRequiresPartialDiagnosticId = "CN0005";
	private const string NetworkPropertyAttributeRequiresGetAndSetDiagnosticId = "CN0006";

	private static readonly DiagnosticDescriptor InvalidNetworkPropertyAttributeRule = new(
		InvalidNetworkPropertyAttributeDiagnosticId,
		"NetworkPropertyAttribute can only be used in NetworkObject-derived types",
		"Property '{0}' is marked with NetworkPropertyAttribute but its containing type does not inherit NetworkObject",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	private static readonly DiagnosticDescriptor NetworkPropertyAttributeRequiresPartialRule = new(
		NetworkPropertyAttributeRequiresPartialDiagnosticId,
		"NetworkPropertyAttribute requires a partial property",
		"Property '{0}' is marked with NetworkPropertyAttribute but is not partial",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	private static readonly DiagnosticDescriptor NetworkPropertyAttributeRequiresGetAndSetRule = new(
		NetworkPropertyAttributeRequiresGetAndSetDiagnosticId,
		"NetworkPropertyAttribute requires get and set accessors",
		"Property '{0}' is marked with NetworkPropertyAttribute but does not have both get and set accessors",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	public static ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [
		InvalidNetworkPropertyAttributeRule,
		NetworkPropertyAttributeRequiresPartialRule,
		NetworkPropertyAttributeRequiresGetAndSetRule
	];

	public static void Register(CompilationStartAnalysisContext context, INamedTypeSymbol networkObjectType, INamedTypeSymbol networkPropertyAttributeType) {
		context.RegisterSymbolAction(
			symbolContext => Analyze(symbolContext, networkObjectType, networkPropertyAttributeType),
			SymbolKind.Property);
	}

	private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol networkObjectType, INamedTypeSymbol networkPropertyAttributeType) {
		IPropertySymbol property = (IPropertySymbol)context.Symbol;

		if (!NetworkAnalyzerHelpers.HasAttribute(property, networkPropertyAttributeType)) {
			return;
		}

		INamedTypeSymbol containingType = property.ContainingType;

		if (!SymbolEqualityComparer.Default.Equals(containingType, networkObjectType) && !NetworkAnalyzerHelpers.InheritsFrom(containingType, networkObjectType)) {
			context.ReportDiagnostic(Diagnostic.Create(
				InvalidNetworkPropertyAttributeRule,
				property.Locations.FirstOrDefault(),
				property.Name));
			return;
		}

		if (!NetworkAnalyzerHelpers.IsPartial(property, context.CancellationToken)) {
			context.ReportDiagnostic(Diagnostic.Create(
				NetworkPropertyAttributeRequiresPartialRule,
				property.Locations.FirstOrDefault(),
				property.Name));
		}

		if (property.GetMethod is null || property.SetMethod is null) {
			context.ReportDiagnostic(Diagnostic.Create(
				NetworkPropertyAttributeRequiresGetAndSetRule,
				property.Locations.FirstOrDefault(),
				property.Name));
		}
	}
}
