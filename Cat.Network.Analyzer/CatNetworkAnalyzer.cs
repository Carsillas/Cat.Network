using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cat.Network.Analyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CatNetworkAnalyzer : DiagnosticAnalyzer {
	private const string MissingNetworkEntityAttributeDiagnosticId = "CN0001";
	private const string InvalidNetworkEntityAttributeDiagnosticId = "CN0002";
	private const string NetworkEntityAttributeRequiresPartialDiagnosticId = "CN0003";
	private const string InvalidNetworkPropertyAttributeDiagnosticId = "CN0004";
	private const string NetworkPropertyAttributeRequiresPartialDiagnosticId = "CN0005";
	private const string NetworkEntityMetadataName = "Cat.Network.NetworkEntity";
	private const string NetworkEntityAttributeMetadataName = "Cat.Network.NetworkEntityAttribute";
	private const string NetworkPropertyAttributeMetadataName = "Cat.Network.NetworkPropertyAttribute";

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

	private static readonly DiagnosticDescriptor InvalidNetworkPropertyAttributeRule = new(
		InvalidNetworkPropertyAttributeDiagnosticId,
		"NetworkPropertyAttribute can only be used in NetworkEntity-derived types",
		"Property '{0}' is marked with NetworkPropertyAttribute but its containing type does not inherit NetworkEntity",
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

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [
		MissingNetworkEntityAttributeRule,
		InvalidNetworkEntityAttributeRule,
		NetworkEntityAttributeRequiresPartialRule,
		InvalidNetworkPropertyAttributeRule,
		NetworkPropertyAttributeRequiresPartialRule
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

			compilationContext.RegisterSymbolAction(
				symbolContext => AnalyzeNamedType(symbolContext, networkEntityType, networkEntityAttributeType),
				SymbolKind.NamedType);

			compilationContext.RegisterSymbolAction(
				symbolContext => AnalyzeProperty(symbolContext, networkEntityType, networkPropertyAttributeType),
				SymbolKind.Property);
		});
	}

	private static void AnalyzeNamedType(SymbolAnalysisContext context, INamedTypeSymbol networkEntityType, INamedTypeSymbol networkEntityAttributeType) {
		INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;

		if (type.TypeKind != TypeKind.Class || SymbolEqualityComparer.Default.Equals(type, networkEntityType)) {
			return;
		}

		bool inheritsNetworkEntity = InheritsFrom(type, networkEntityType);
		bool hasNetworkEntityAttribute = HasAttribute(type, networkEntityAttributeType);

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

		if (inheritsNetworkEntity && hasNetworkEntityAttribute && !IsPartial(type, context.CancellationToken)) {
			context.ReportDiagnostic(Diagnostic.Create(
				NetworkEntityAttributeRequiresPartialRule,
				type.Locations.FirstOrDefault(),
				type.Name));
		}
	}

	private static void AnalyzeProperty(SymbolAnalysisContext context, INamedTypeSymbol networkEntityType, INamedTypeSymbol networkPropertyAttributeType) {
		IPropertySymbol property = (IPropertySymbol)context.Symbol;

		if (!HasAttribute(property, networkPropertyAttributeType)) {
			return;
		}

		INamedTypeSymbol containingType = property.ContainingType;

		if (SymbolEqualityComparer.Default.Equals(containingType, networkEntityType) || InheritsFrom(containingType, networkEntityType)) {
			if (!IsPartial(property, context.CancellationToken)) {
				context.ReportDiagnostic(Diagnostic.Create(
					NetworkPropertyAttributeRequiresPartialRule,
					property.Locations.FirstOrDefault(),
					property.Name));
			}

			return;
		}

		context.ReportDiagnostic(Diagnostic.Create(
			InvalidNetworkPropertyAttributeRule,
			property.Locations.FirstOrDefault(),
			property.Name));
	}

	private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType) {
		for (INamedTypeSymbol? current = type.BaseType; current is not null; current = current.BaseType)
			if (SymbolEqualityComparer.Default.Equals(current, baseType)) {
				return true;
			}

		return false;
	}

	private static bool HasAttribute(INamedTypeSymbol type, INamedTypeSymbol attributeType) {
		return type.GetAttributes().Any(attribute =>
			SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType));
	}

	private static bool HasAttribute(IPropertySymbol property, INamedTypeSymbol attributeType) {
		return property.GetAttributes().Any(attribute =>
			SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType));
	}

	private static bool IsPartial(INamedTypeSymbol type, CancellationToken cancellationToken) {
		return type.DeclaringSyntaxReferences
			.Select(reference => reference.GetSyntax(cancellationToken))
			.OfType<TypeDeclarationSyntax>()
			.Any(declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
	}

	private static bool IsPartial(IPropertySymbol property, CancellationToken cancellationToken) {
		return property.DeclaringSyntaxReferences
			.Select(reference => reference.GetSyntax(cancellationToken))
			.OfType<PropertyDeclarationSyntax>()
			.Any(declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
	}
}
