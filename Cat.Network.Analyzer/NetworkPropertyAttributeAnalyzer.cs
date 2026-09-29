using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cat.Network.Analyzer;

internal static class NetworkPropertyAttributeAnalyzer {
	private const string InvalidNetworkPropertyAttributeDiagnosticId = "CN0004";
	private const string NetworkPropertyAttributeRequiresPartialDiagnosticId = "CN0005";
	private const string NetworkPropertyAttributeRequiresGetAndSetDiagnosticId = "CN0006";
	private const string NetworkPropertyAttributeCannotUseNetworkEntityTypeDiagnosticId = "CN0027";
	private const string NetworkPropertyAttributeRequiresSupportedTypeDiagnosticId = "CN0028";

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

	private static readonly DiagnosticDescriptor NetworkPropertyAttributeCannotUseNetworkEntityTypeRule = new(
		NetworkPropertyAttributeCannotUseNetworkEntityTypeDiagnosticId,
		"NetworkPropertyAttribute type must not be assignable from NetworkEntity-derived types",
		"Property '{0}' is marked with NetworkPropertyAttribute but type '{1}' is assignable from NetworkEntity-derived types",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	private static readonly DiagnosticDescriptor NetworkPropertyAttributeRequiresSupportedTypeRule = new(
		NetworkPropertyAttributeRequiresSupportedTypeDiagnosticId,
		"NetworkPropertyAttribute requires a supported property type",
		"Property '{0}' is marked with NetworkPropertyAttribute but type '{1}' is not supported by network property serialization",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	public static ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [
		InvalidNetworkPropertyAttributeRule,
		NetworkPropertyAttributeRequiresPartialRule,
		NetworkPropertyAttributeRequiresGetAndSetRule,
		NetworkPropertyAttributeCannotUseNetworkEntityTypeRule,
		NetworkPropertyAttributeRequiresSupportedTypeRule
	];

	public static void Register(CompilationStartAnalysisContext context, INamedTypeSymbol networkObjectType, INamedTypeSymbol? networkEntityType, INamedTypeSymbol networkPropertyAttributeType) {
		context.RegisterSymbolAction(
			symbolContext => Analyze(symbolContext, networkObjectType, networkEntityType, networkPropertyAttributeType),
			SymbolKind.Property);
	}

	private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol networkObjectType, INamedTypeSymbol? networkEntityType, INamedTypeSymbol networkPropertyAttributeType) {
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

		if (property.Type is INamedTypeSymbol propertyType &&
		    IsNetworkEntityCompatibleType(propertyType, networkObjectType, networkEntityType)) {
			context.ReportDiagnostic(Diagnostic.Create(
				NetworkPropertyAttributeCannotUseNetworkEntityTypeRule,
				property.Locations.FirstOrDefault(),
				property.Name,
				propertyType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
		}

		if (!IsSupportedNetworkPropertyType(property.Type, networkObjectType, ImmutableHashSet<ITypeSymbol>.Empty.WithComparer(SymbolEqualityComparer.Default))) {
			context.ReportDiagnostic(Diagnostic.Create(
				NetworkPropertyAttributeRequiresSupportedTypeRule,
				property.Locations.FirstOrDefault(),
				property.Name,
				property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
		}
	}

	private static bool IsNetworkEntityCompatibleType(INamedTypeSymbol type, INamedTypeSymbol networkObjectType, INamedTypeSymbol? networkEntityType) {
		if (SymbolEqualityComparer.Default.Equals(type, networkObjectType)) {
			return true;
		}

		if (networkEntityType is null) {
			return false;
		}

		return SymbolEqualityComparer.Default.Equals(type, networkEntityType) ||
		       NetworkAnalyzerHelpers.InheritsFrom(type, networkEntityType);
	}

	private static bool IsSupportedNetworkPropertyType(ITypeSymbol type, INamedTypeSymbol networkObjectType, ImmutableHashSet<ITypeSymbol> visitedTypes) {
		if (IsSupportedScalarOrStringOrGuidType(type)) {
			return true;
		}

		if (type is INamedTypeSymbol namedType &&
		    namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
		    namedType.TypeArguments.Length == 1) {
			type = namedType.TypeArguments[0];
		}

		if (type.TypeKind == TypeKind.Struct && type is INamedTypeSymbol structType) {
			if (visitedTypes.Contains(structType)) {
				return false;
			}

			ImmutableHashSet<ITypeSymbol> nextVisitedTypes = visitedTypes.Add(structType);
			return structType.GetMembers()
				.OfType<IFieldSymbol>()
				.Where(static field => !field.IsStatic && field.DeclaredAccessibility == Accessibility.Public)
				.All(field => IsSupportedNetworkPropertyType(field.Type, networkObjectType, nextVisitedTypes) &&
				              !IsNetworkObjectCompatibleType(field.Type, networkObjectType));
		}

		return IsNetworkObjectCompatibleType(type, networkObjectType);
	}

	private static bool IsNetworkObjectCompatibleType(ITypeSymbol type, INamedTypeSymbol networkObjectType) {
		return type is INamedTypeSymbol namedType &&
		       (SymbolEqualityComparer.Default.Equals(namedType, networkObjectType) ||
		        NetworkAnalyzerHelpers.InheritsFrom(namedType, networkObjectType));
	}

	private static bool IsSupportedScalarOrStringOrGuidType(ITypeSymbol type) {
		if (type.SpecialType is SpecialType.System_Boolean or
		    SpecialType.System_Byte or
		    SpecialType.System_SByte or
		    SpecialType.System_Int16 or
		    SpecialType.System_UInt16 or
		    SpecialType.System_Int32 or
		    SpecialType.System_UInt32 or
		    SpecialType.System_Int64 or
		    SpecialType.System_UInt64 or
		    SpecialType.System_Single or
		    SpecialType.System_Double or
		    SpecialType.System_String) {
			return true;
		}

		return type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Guid";
	}
}
