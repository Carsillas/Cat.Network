using System.Collections.Immutable;
using System.Linq;
using Cat.Network.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cat.Network.Analyzer;

internal static class NetworkMemberNameAnalyzer {
	private static readonly DiagnosticDescriptor DuplicateInheritedNetworkMemberNameRule = new(
		"CN0007",
		"Network properties and collections cannot hide inherited network members",
		"Member '{0}' conflicts with inherited network member '{0}' declared on '{1}'. Network property and collection names must be unique across the inheritance chain.",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	public static ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [DuplicateInheritedNetworkMemberNameRule];

	public static void Register(CompilationStartAnalysisContext context, INamedTypeSymbol networkObjectType) {
		context.RegisterSymbolAction(symbolContext => Analyze(symbolContext, networkObjectType), SymbolKind.Property);
	}

	private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol networkObjectType) {
		IPropertySymbol property = (IPropertySymbol)context.Symbol;
		if (!NetworkMemberNames.IsNetworkMember(property) ||
		    (!SymbolEqualityComparer.Default.Equals(property.ContainingType, networkObjectType) &&
		     !NetworkAnalyzerHelpers.InheritsFrom(property.ContainingType, networkObjectType))) {
			return;
		}

		IPropertySymbol? inheritedMember = NetworkMemberNames.FindInheritedMemberWithSameName(property, context.CancellationToken);
		if (inheritedMember is not null) {
			context.ReportDiagnostic(Diagnostic.Create(
				DuplicateInheritedNetworkMemberNameRule,
				property.Locations.FirstOrDefault(),
				property.Name,
				inheritedMember.ContainingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
		}
	}
}
