using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cat.Network.Analyzer;

internal static class UpgradeToAttributeAnalyzer {
	private const string InvalidUpgradeToAttributeDiagnosticId = "CN0017";
	private const string UpgradeToMethodRequiresValidSignatureDiagnosticId = "CN0018";
	private const string DuplicateUpgradeToVersionDiagnosticId = "CN0019";
	private const string UpgradeToVersionMustBeInRangeDiagnosticId = "CN0020";

	private static readonly DiagnosticDescriptor InvalidUpgradeToAttributeRule = new(
		InvalidUpgradeToAttributeDiagnosticId,
		"UpgradeToAttribute can only be used in NetworkObject-derived types",
		"Method '{0}' is marked with UpgradeToAttribute but its containing type does not inherit NetworkObject",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	private static readonly DiagnosticDescriptor UpgradeToMethodRequiresValidSignatureRule = new(
		UpgradeToMethodRequiresValidSignatureDiagnosticId,
		"UpgradeToAttribute requires a static, non-async upgrade method with the expected signature",
		"Upgrade method '{0}' must be static, non-async, non-generic, return void, and accept (NetworkObjectUpgradeReader reader, NetworkObjectUpgradeWriter writer)",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	private static readonly DiagnosticDescriptor DuplicateUpgradeToVersionRule = new(
		DuplicateUpgradeToVersionDiagnosticId,
		"UpgradeToAttribute target versions must be unique",
		"NetworkObject type '{0}' declares more than one upgrade method targeting version {1}",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	private static readonly DiagnosticDescriptor UpgradeToVersionMustBeInRangeRule = new(
		UpgradeToVersionMustBeInRangeDiagnosticId,
		"UpgradeToAttribute target version must be supported by the NetworkObject schema version",
		"Upgrade method '{0}' targets version {1}, but NetworkObject type '{2}' declares schema version {3}",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	public static ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [
		InvalidUpgradeToAttributeRule,
		UpgradeToMethodRequiresValidSignatureRule,
		DuplicateUpgradeToVersionRule,
		UpgradeToVersionMustBeInRangeRule
	];

	public static void Register(
		CompilationStartAnalysisContext context,
		INamedTypeSymbol networkObjectType,
		INamedTypeSymbol networkObjectAttributeType,
		INamedTypeSymbol upgradeToAttributeType,
		INamedTypeSymbol upgradeReaderType,
		INamedTypeSymbol upgradeWriterType) {
		context.RegisterSymbolAction(
			symbolContext => Analyze(symbolContext, networkObjectType, networkObjectAttributeType, upgradeToAttributeType, upgradeReaderType, upgradeWriterType),
			SymbolKind.NamedType);
	}

	private static void Analyze(
		SymbolAnalysisContext context,
		INamedTypeSymbol networkObjectType,
		INamedTypeSymbol networkObjectAttributeType,
		INamedTypeSymbol upgradeToAttributeType,
		INamedTypeSymbol upgradeReaderType,
		INamedTypeSymbol upgradeWriterType) {
		INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;
		ImmutableArray<IMethodSymbol> upgradeMethods = type.GetMembers()
			.OfType<IMethodSymbol>()
			.Where(method => NetworkAnalyzerHelpers.HasAttribute(method, upgradeToAttributeType))
			.ToImmutableArray();

		if (upgradeMethods.IsDefaultOrEmpty) {
			return;
		}

		bool inheritsNetworkObject = NetworkAnalyzerHelpers.InheritsFrom(type, networkObjectType);
		if (!inheritsNetworkObject) {
			foreach (IMethodSymbol method in upgradeMethods) {
				context.ReportDiagnostic(Diagnostic.Create(
					InvalidUpgradeToAttributeRule,
					method.Locations.FirstOrDefault(),
					method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
			}

			return;
		}

		foreach (IMethodSymbol method in upgradeMethods) {
			if (!HasExpectedSignature(method, upgradeReaderType, upgradeWriterType)) {
				context.ReportDiagnostic(Diagnostic.Create(
					UpgradeToMethodRequiresValidSignatureRule,
					method.Locations.FirstOrDefault(),
					method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
			}
		}

		bool hasNetworkObjectAttribute = NetworkAnalyzerHelpers.HasAttribute(type, networkObjectAttributeType);
		ushort schemaVersion = hasNetworkObjectAttribute ? GetSchemaVersion(type, networkObjectAttributeType) : (ushort)0;
		if (hasNetworkObjectAttribute) {
			foreach (IMethodSymbol method in upgradeMethods) {
				ushort targetVersion = GetTargetVersion(method, upgradeToAttributeType);
				if (targetVersion == 0 || targetVersion > schemaVersion) {
					context.ReportDiagnostic(Diagnostic.Create(
						UpgradeToVersionMustBeInRangeRule,
						method.Locations.FirstOrDefault(),
						method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
						targetVersion,
						type.Name,
						schemaVersion));
				}
			}
		}

		foreach (IGrouping<ushort, IMethodSymbol> duplicateGroup in upgradeMethods
			         .GroupBy(method => GetTargetVersion(method, upgradeToAttributeType))
			         .Where(static group => group.Count() > 1)) {
			foreach (IMethodSymbol method in duplicateGroup) {
				context.ReportDiagnostic(Diagnostic.Create(
					DuplicateUpgradeToVersionRule,
					method.Locations.FirstOrDefault(),
					type.Name,
					duplicateGroup.Key));
			}
		}
	}

	private static bool HasExpectedSignature(IMethodSymbol method, INamedTypeSymbol upgradeReaderType, INamedTypeSymbol upgradeWriterType) {
		// Partial method declarations do not carry the implementation's async modifier.
		return method.IsStatic &&
		       !method.IsAsync &&
		       method.PartialImplementationPart?.IsAsync != true &&
		       !method.IsGenericMethod &&
		       method.ReturnsVoid &&
		       method.Parameters.Length == 2 &&
		       method.Parameters[0].RefKind == RefKind.None &&
		       SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, upgradeReaderType) &&
		       method.Parameters[1].RefKind == RefKind.None &&
		       SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, upgradeWriterType);
	}

	private static ushort GetSchemaVersion(INamedTypeSymbol type, INamedTypeSymbol networkObjectAttributeType) {
		AttributeData? attribute = type.GetAttributes()
			.FirstOrDefault(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, networkObjectAttributeType));
		if (attribute is null) {
			return 0;
		}

		foreach (KeyValuePair<string, TypedConstant> namedArgument in attribute.NamedArguments) {
			if (namedArgument.Key == "Version" && namedArgument.Value.Value is ushort version) {
				return version;
			}
		}

		return 0;
	}

	private static ushort GetTargetVersion(IMethodSymbol method, INamedTypeSymbol upgradeToAttributeType) {
		AttributeData? attribute = method.GetAttributes()
			.FirstOrDefault(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, upgradeToAttributeType));
		if (attribute?.ConstructorArguments.Length == 1 &&
		    attribute.ConstructorArguments[0].Value is ushort version) {
			return version;
		}

		return 0;
	}
}
