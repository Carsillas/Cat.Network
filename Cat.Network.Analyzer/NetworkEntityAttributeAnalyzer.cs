using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cat.Network.Analyzer;

internal static class NetworkEntityAttributeAnalyzer {
	private const string MissingNetworkEntityAttributeDiagnosticId = "CN0001";
	private const string InvalidNetworkEntityAttributeDiagnosticId = "CN0002";
	private const string NetworkEntityAttributeRequiresPartialDiagnosticId = "CN0003";

	private static readonly DiagnosticDescriptor MissingNetworkEntityAttributeRule = new(
		MissingNetworkEntityAttributeDiagnosticId,
		"NetworkEntity-derived type must be marked with NetworkEntityAttribute",
		"Type '{0}' inherits NetworkEntity but is not marked with NetworkEntityAttribute",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	private static readonly DiagnosticDescriptor InvalidNetworkEntityAttributeRule = new(
		InvalidNetworkEntityAttributeDiagnosticId,
		"NetworkEntityAttribute can only be used on NetworkEntity-derived types",
		"Type '{0}' is marked with NetworkEntityAttribute but does not inherit NetworkEntity",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	private static readonly DiagnosticDescriptor NetworkEntityAttributeRequiresPartialRule = new(
		NetworkEntityAttributeRequiresPartialDiagnosticId,
		"NetworkEntityAttribute requires a partial type",
		"Type '{0}' is marked with NetworkEntityAttribute but is not partial",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	public static ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [
		MissingNetworkEntityAttributeRule,
		InvalidNetworkEntityAttributeRule,
		NetworkEntityAttributeRequiresPartialRule
	];

	public static void Register(CompilationStartAnalysisContext context, INamedTypeSymbol networkEntityType, INamedTypeSymbol networkEntityAttributeType) {
		context.RegisterSymbolAction(
			symbolContext => Analyze(symbolContext, networkEntityType, networkEntityAttributeType),
			SymbolKind.NamedType);
	}

	private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol networkEntityType, INamedTypeSymbol networkEntityAttributeType) {
		INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;

		if (type.TypeKind != TypeKind.Class || SymbolEqualityComparer.Default.Equals(type, networkEntityType)) {
			return;
		}

		bool inheritsNetworkEntity = NetworkAnalyzerHelpers.InheritsFrom(type, networkEntityType);
		bool hasNetworkEntityAttribute = NetworkAnalyzerHelpers.HasAttribute(type, networkEntityAttributeType);

		if (inheritsNetworkEntity && !hasNetworkEntityAttribute) {
			context.ReportDiagnostic(Diagnostic.Create(
				MissingNetworkEntityAttributeRule,
				type.Locations.FirstOrDefault(),
				type.Name));
		}

		if (!inheritsNetworkEntity && hasNetworkEntityAttribute) {
			context.ReportDiagnostic(Diagnostic.Create(
				InvalidNetworkEntityAttributeRule,
				type.Locations.FirstOrDefault(),
				type.Name));
		}

		if (inheritsNetworkEntity && hasNetworkEntityAttribute && !NetworkAnalyzerHelpers.IsPartial(type, context.CancellationToken)) {
			context.ReportDiagnostic(Diagnostic.Create(
				NetworkEntityAttributeRequiresPartialRule,
				type.Locations.FirstOrDefault(),
				type.Name));
		}
	}
}
