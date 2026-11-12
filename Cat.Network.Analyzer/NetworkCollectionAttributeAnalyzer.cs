using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cat.Network.Analyzer;

internal static class NetworkCollectionAttributeAnalyzer {
	private const string InvalidNetworkCollectionAttributeDiagnosticId = "CN0010";
	private const string NetworkCollectionAttributeRequiresPartialDiagnosticId = "CN0011";
	private const string NetworkCollectionAttributeRequiresGetterOnlyDiagnosticId = "CN0012";
	private const string NetworkCollectionAttributeRequiresCollectionTypeDiagnosticId = "CN0013";
	private const string NetworkCollectionAttributeCannotBeInitializedDiagnosticId = "CN0014";
	private const string NetworkCollectionAttributeRequiresSupportedItemTypeDiagnosticId = "CN0015";
	private const string NetworkCollectionAttributeRequiresSupportedKeyTypeDiagnosticId = "CN0016";
	private const string NetworkCollectionTypeRequiresNetworkCollectionAttributeDiagnosticId = "CN0026";

	private const string NetworkListMetadataName = "global::Cat.Network.NetworkList<T>";
	private const string NetworkDictionaryMetadataName = "global::Cat.Network.NetworkDictionary<TKey, TValue>";

	private static readonly DiagnosticDescriptor InvalidNetworkCollectionAttributeRule = new(
		InvalidNetworkCollectionAttributeDiagnosticId,
		"NetworkCollectionAttribute can only be used in NetworkObject-derived types",
		"Property '{0}' is marked with NetworkCollectionAttribute but its containing type does not inherit NetworkObject",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	private static readonly DiagnosticDescriptor NetworkCollectionAttributeRequiresPartialRule = new(
		NetworkCollectionAttributeRequiresPartialDiagnosticId,
		"NetworkCollectionAttribute requires a partial property",
		"Property '{0}' is marked with NetworkCollectionAttribute but is not partial",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	private static readonly DiagnosticDescriptor NetworkCollectionAttributeRequiresGetterOnlyRule = new(
		NetworkCollectionAttributeRequiresGetterOnlyDiagnosticId,
		"NetworkCollectionAttribute requires a getter-only property",
		"Property '{0}' is marked with NetworkCollectionAttribute but must be getter-only",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	private static readonly DiagnosticDescriptor NetworkCollectionAttributeRequiresCollectionTypeRule = new(
		NetworkCollectionAttributeRequiresCollectionTypeDiagnosticId,
		"NetworkCollectionAttribute requires NetworkList<T> or NetworkDictionary<TKey, TValue>",
		"Property '{0}' is marked with NetworkCollectionAttribute but is not of type NetworkList<T> or NetworkDictionary<TKey, TValue>",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	private static readonly DiagnosticDescriptor NetworkCollectionAttributeCannotBeInitializedRule = new(
		NetworkCollectionAttributeCannotBeInitializedDiagnosticId,
		"NetworkCollectionAttribute properties cannot declare an initializer",
		"Property '{0}' is marked with NetworkCollectionAttribute but already declares an initializer",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	private static readonly DiagnosticDescriptor NetworkCollectionAttributeRequiresSupportedItemTypeRule = new(
		NetworkCollectionAttributeRequiresSupportedItemTypeDiagnosticId,
		"NetworkCollectionAttribute requires a supported item type",
		"Property '{0}' is marked with NetworkCollectionAttribute but item type '{1}' is not supported by network property serialization",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	private static readonly DiagnosticDescriptor NetworkCollectionAttributeRequiresSupportedKeyTypeRule = new(
		NetworkCollectionAttributeRequiresSupportedKeyTypeDiagnosticId,
		"NetworkCollectionAttribute requires a supported dictionary key type",
		"Property '{0}' is marked with NetworkCollectionAttribute but key type '{1}' is not supported for network dictionary serialization",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	private static readonly DiagnosticDescriptor NetworkCollectionTypeRequiresNetworkCollectionAttributeRule = new(
		NetworkCollectionTypeRequiresNetworkCollectionAttributeDiagnosticId,
		"NetworkList<T> and NetworkDictionary<TKey, TValue> require NetworkCollectionAttribute",
		"Property '{0}' is of type '{1}' but is not marked with NetworkCollectionAttribute",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	public static ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [
		InvalidNetworkCollectionAttributeRule,
		NetworkCollectionAttributeRequiresPartialRule,
		NetworkCollectionAttributeRequiresGetterOnlyRule,
		NetworkCollectionAttributeRequiresCollectionTypeRule,
		NetworkCollectionAttributeCannotBeInitializedRule,
		NetworkCollectionAttributeRequiresSupportedItemTypeRule,
		NetworkCollectionAttributeRequiresSupportedKeyTypeRule,
		NetworkCollectionTypeRequiresNetworkCollectionAttributeRule
	];

	public static void Register(CompilationStartAnalysisContext context, INamedTypeSymbol networkObjectType, INamedTypeSymbol networkCollectionAttributeType) {
		context.RegisterSymbolAction(
			symbolContext => Analyze(symbolContext, networkObjectType, networkCollectionAttributeType),
			SymbolKind.Property);
	}

	private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol networkObjectType, INamedTypeSymbol networkCollectionAttributeType) {
		IPropertySymbol property = (IPropertySymbol)context.Symbol;
		bool hasNetworkCollectionAttribute = NetworkAnalyzerHelpers.HasAttribute(property, networkCollectionAttributeType);

		if (!hasNetworkCollectionAttribute) {
			if (IsNetworkCollectionType(property.Type) &&
			    IsNetworkObjectOrDerived(property.ContainingType, networkObjectType)) {
				context.ReportDiagnostic(Diagnostic.Create(
					NetworkCollectionTypeRequiresNetworkCollectionAttributeRule,
					property.Locations.FirstOrDefault(),
					property.Name,
					property.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
			}

			return;
		}

		INamedTypeSymbol containingType = property.ContainingType;
		if (!IsNetworkObjectOrDerived(containingType, networkObjectType)) {
			context.ReportDiagnostic(Diagnostic.Create(
				InvalidNetworkCollectionAttributeRule,
				property.Locations.FirstOrDefault(),
				property.Name));
			return;
		}

		if (!NetworkAnalyzerHelpers.IsPartial(property, context.CancellationToken)) {
			context.ReportDiagnostic(Diagnostic.Create(
				NetworkCollectionAttributeRequiresPartialRule,
				property.Locations.FirstOrDefault(),
				property.Name));
		}

		if (property.GetMethod is null || property.SetMethod is not null) {
			context.ReportDiagnostic(Diagnostic.Create(
				NetworkCollectionAttributeRequiresGetterOnlyRule,
				property.Locations.FirstOrDefault(),
				property.Name));
		}

		if (property.Type is not INamedTypeSymbol propertyType) {
			context.ReportDiagnostic(Diagnostic.Create(
				NetworkCollectionAttributeRequiresCollectionTypeRule,
				property.Locations.FirstOrDefault(),
				property.Name));
		} else if (IsNetworkListType(propertyType)) {
			if (!IsSupportedCollectionValueType(propertyType.TypeArguments[0], networkObjectType, context.CancellationToken)) {
				context.ReportDiagnostic(Diagnostic.Create(
					NetworkCollectionAttributeRequiresSupportedItemTypeRule,
					property.Locations.FirstOrDefault(),
					property.Name,
					propertyType.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
			}
		} else if (IsNetworkDictionaryType(propertyType)) {
			if (!IsSupportedDictionaryKeyType(propertyType.TypeArguments[0])) {
				context.ReportDiagnostic(Diagnostic.Create(
					NetworkCollectionAttributeRequiresSupportedKeyTypeRule,
					property.Locations.FirstOrDefault(),
					property.Name,
					propertyType.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
			}
			if (!IsSupportedCollectionValueType(propertyType.TypeArguments[1], networkObjectType, context.CancellationToken)) {
				context.ReportDiagnostic(Diagnostic.Create(
					NetworkCollectionAttributeRequiresSupportedItemTypeRule,
					property.Locations.FirstOrDefault(),
					property.Name,
					propertyType.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
			}
		} else {
			context.ReportDiagnostic(Diagnostic.Create(
				NetworkCollectionAttributeRequiresCollectionTypeRule,
				property.Locations.FirstOrDefault(),
				property.Name));
		}

		bool hasInitializer = property.DeclaringSyntaxReferences
			.Select(reference => reference.GetSyntax(context.CancellationToken))
			.OfType<PropertyDeclarationSyntax>()
			.Any(static declaration => declaration.Initializer is not null);
		if (hasInitializer) {
			context.ReportDiagnostic(Diagnostic.Create(
				NetworkCollectionAttributeCannotBeInitializedRule,
				property.Locations.FirstOrDefault(),
				property.Name));
		}
	}

	private static bool IsNetworkObjectOrDerived(INamedTypeSymbol type, INamedTypeSymbol networkObjectType) {
		return SymbolEqualityComparer.Default.Equals(type, networkObjectType) ||
		       NetworkAnalyzerHelpers.InheritsFrom(type, networkObjectType);
	}

	private static bool IsNetworkCollectionType(ITypeSymbol type) {
		return type is INamedTypeSymbol namedType &&
		       (IsNetworkListType(namedType) || IsNetworkDictionaryType(namedType));
	}

	private static bool IsNetworkListType(INamedTypeSymbol type) {
		return type.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == NetworkListMetadataName;
	}

	private static bool IsNetworkDictionaryType(INamedTypeSymbol type) {
		return type.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == NetworkDictionaryMetadataName;
	}

	private static bool IsSupportedCollectionValueType(ITypeSymbol type, INamedTypeSymbol networkObjectType, CancellationToken cancellationToken) {
		cancellationToken.ThrowIfCancellationRequested();

		if (IsSupportedScalarOrStringOrGuidType(type)) {
			return true;
		}

		if (type is INamedTypeSymbol namedType &&
		    namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
		    namedType.TypeArguments.Length == 1) {
			type = namedType.TypeArguments[0];
		}

		if (type.TypeKind == TypeKind.Struct && type is INamedTypeSymbol structType) {
			ImmutableHashSet<ITypeSymbol> visitedTypes = ImmutableHashSet<ITypeSymbol>.Empty.WithComparer(SymbolEqualityComparer.Default).Add(structType);
			return structType.GetMembers()
				.OfType<IFieldSymbol>()
				.Where(static field => !field.IsStatic && field.DeclaredAccessibility == Accessibility.Public)
				.All(field => IsSupportedStructFieldType(field.Type, networkObjectType, visitedTypes, cancellationToken));
		}

		return SymbolEqualityComparer.Default.Equals(type, networkObjectType) ||
		       NetworkAnalyzerHelpers.InheritsFrom((INamedTypeSymbol)type, networkObjectType);
	}

	private static bool IsSupportedDictionaryKeyType(ITypeSymbol type) {
		if (type is INamedTypeSymbol namedType &&
		    namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
		    namedType.TypeArguments.Length == 1) {
			return false;
		}

		if (IsSupportedScalarOrStringOrGuidType(type)) {
			return true;
		}

		if (type.TypeKind == TypeKind.Struct && type is INamedTypeSymbol structType) {
			return structType.GetMembers()
				.OfType<IFieldSymbol>()
				.Where(static field => !field.IsStatic && field.DeclaredAccessibility == Accessibility.Public)
				.All(IsSupportedDictionaryKeyFieldType);
		}

		return false;
	}

	private static bool IsSupportedDictionaryKeyFieldType(IFieldSymbol field) {
		if (field.Type is INamedTypeSymbol namedType &&
		    namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
		    namedType.TypeArguments.Length == 1) {
			return false;
		}

		if (IsSupportedScalarOrStringOrGuidType(field.Type)) {
			return true;
		}

		if (field.Type.TypeKind == TypeKind.Struct && field.Type is INamedTypeSymbol structType) {
			return structType.GetMembers()
				.OfType<IFieldSymbol>()
				.Where(static nestedField => !nestedField.IsStatic && nestedField.DeclaredAccessibility == Accessibility.Public)
				.All(IsSupportedDictionaryKeyFieldType);
		}

		return false;
	}

	private static bool IsSupportedStructFieldType(ITypeSymbol type, INamedTypeSymbol networkObjectType, ImmutableHashSet<ITypeSymbol> visitedTypes, CancellationToken cancellationToken) {
		cancellationToken.ThrowIfCancellationRequested();

		if (type is INamedTypeSymbol namedType &&
		    namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
		    namedType.TypeArguments.Length == 1) {
			type = namedType.TypeArguments[0];
		}

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

		if (type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Guid") {
			return true;
		}

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

		if (type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Guid") {
			return true;
		}

		if (type.TypeKind == TypeKind.Struct && type is INamedTypeSymbol structType) {
			if (visitedTypes.Count >= NetworkAnalyzerHelpers.MaxStructNestingDepth || visitedTypes.Contains(structType)) {
				return false;
			}

			ImmutableHashSet<ITypeSymbol> nextVisitedTypes = visitedTypes.Add(structType);
			return structType.GetMembers()
				.OfType<IFieldSymbol>()
				.Where(static field => !field.IsStatic && field.DeclaredAccessibility == Accessibility.Public)
				.All(field => IsSupportedStructFieldType(field.Type, networkObjectType, nextVisitedTypes, cancellationToken));
		}

		return false;
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
