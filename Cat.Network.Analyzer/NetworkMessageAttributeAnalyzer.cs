using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cat.Network.Analyzer;

internal static class NetworkMessageAttributeAnalyzer {
	private const string InvalidNetworkMessageAttributeDiagnosticId = "CN0021";
	private const string NetworkMessageAttributeRequiresPartialVoidDiagnosticId = "CN0022";
	private const string NetworkMessageAttributeParameterUnsupportedDiagnosticId = "CN0023";
	private const string NetworkMessageAttributeParameterCannotBeEntityDiagnosticId = "CN0024";
	private const string NetworkMessageExplicitHandlerMustBeExplicitDiagnosticId = "CN0025";

	private static readonly DiagnosticDescriptor InvalidNetworkMessageAttributeRule = new(
		InvalidNetworkMessageAttributeDiagnosticId,
		"Network messages can only be declared in NetworkEntity-derived types",
		"Method '{0}' is marked with {1} but its containing type does not inherit NetworkEntity",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	private static readonly DiagnosticDescriptor NetworkMessageAttributeRequiresPartialVoidRule = new(
		NetworkMessageAttributeRequiresPartialVoidDiagnosticId,
		"Network messages require partial void methods",
		"Method '{0}' is marked with {1} but is not a non-generic partial void method without ref, out, or in parameters",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	private static readonly DiagnosticDescriptor NetworkMessageAttributeParameterUnsupportedRule = new(
		NetworkMessageAttributeParameterUnsupportedDiagnosticId,
		"Network message parameter type is not supported",
		"Parameter '{0}' on network message '{1}' has unsupported type '{2}'",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	private static readonly DiagnosticDescriptor NetworkMessageAttributeParameterCannotBeEntityRule = new(
		NetworkMessageAttributeParameterCannotBeEntityDiagnosticId,
		"Network message parameters cannot be NetworkEntity types",
		"Parameter '{0}' on network message '{1}' uses NetworkEntity type '{2}', which is not supported",
		"Usage",
		DiagnosticSeverity.Error,
		true);

	private static readonly DiagnosticDescriptor NetworkMessageExplicitHandlerMustBeExplicitRule = new(
		NetworkMessageExplicitHandlerMustBeExplicitDiagnosticId,
		"Explicit network message handlers should use explicit interface implementation",
		"Method '{0}' matches the generated {1} receive handler for '{2}' but is public; implement the generated interface method explicitly instead",
		"Usage",
		DiagnosticSeverity.Warning,
		true);

	public static ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [
		InvalidNetworkMessageAttributeRule,
		NetworkMessageAttributeRequiresPartialVoidRule,
		NetworkMessageAttributeParameterUnsupportedRule,
		NetworkMessageAttributeParameterCannotBeEntityRule,
		NetworkMessageExplicitHandlerMustBeExplicitRule
	];

	public static void Register(
		CompilationStartAnalysisContext context,
		INamedTypeSymbol networkObjectType,
		INamedTypeSymbol networkEntityType,
		INamedTypeSymbol relayClientType,
		INamedTypeSymbol networkProfileType,
		INamedTypeSymbol rpcAttributeType,
		INamedTypeSymbol broadcastAttributeType) {
		context.RegisterSymbolAction(
			symbolContext => Analyze(symbolContext, networkObjectType, networkEntityType, relayClientType, networkProfileType, rpcAttributeType, broadcastAttributeType),
			SymbolKind.Method);
	}

	private static void Analyze(
		SymbolAnalysisContext context,
		INamedTypeSymbol networkObjectType,
		INamedTypeSymbol networkEntityType,
		INamedTypeSymbol relayClientType,
		INamedTypeSymbol networkProfileType,
		INamedTypeSymbol rpcAttributeType,
		INamedTypeSymbol broadcastAttributeType) {
		IMethodSymbol method = (IMethodSymbol)context.Symbol;
		INamedTypeSymbol? attributeType = NetworkAnalyzerHelpers.HasAttribute(method, rpcAttributeType)
			? rpcAttributeType
			: NetworkAnalyzerHelpers.HasAttribute(method, broadcastAttributeType)
				? broadcastAttributeType
				: null;
		if (attributeType is null) {
			AnalyzeExplicitHandlerImplementation(context, method, networkEntityType, relayClientType, networkProfileType, rpcAttributeType, broadcastAttributeType);
			return;
		}

		if (!NetworkAnalyzerHelpers.InheritsFrom(method.ContainingType, networkEntityType) &&
		    !SymbolEqualityComparer.Default.Equals(method.ContainingType, networkEntityType)) {
			context.ReportDiagnostic(Diagnostic.Create(
				InvalidNetworkMessageAttributeRule,
				method.Locations.FirstOrDefault(),
				method.Name,
				attributeType.Name));
			return;
		}

		if (!NetworkAnalyzerHelpers.IsPartial(method, context.CancellationToken) ||
		    method.ReturnsVoid == false ||
		    method.TypeParameters.Length != 0 ||
		    method.Parameters.Any(static parameter => parameter.RefKind != RefKind.None)) {
			context.ReportDiagnostic(Diagnostic.Create(
				NetworkMessageAttributeRequiresPartialVoidRule,
				method.Locations.FirstOrDefault(),
				method.Name,
				attributeType.Name));
		}

		foreach (IParameterSymbol parameter in method.Parameters) {
			if (IsNetworkEntityType(parameter.Type, networkEntityType)) {
				context.ReportDiagnostic(Diagnostic.Create(
					NetworkMessageAttributeParameterCannotBeEntityRule,
					parameter.Locations.FirstOrDefault(),
					parameter.Name,
					method.Name,
					parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
				continue;
			}

			if (!IsSupportedParameterType(parameter.Type, networkObjectType)) {
				context.ReportDiagnostic(Diagnostic.Create(
					NetworkMessageAttributeParameterUnsupportedRule,
					parameter.Locations.FirstOrDefault(),
					parameter.Name,
					method.Name,
					parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
			}
		}
	}

	private static void AnalyzeExplicitHandlerImplementation(
		SymbolAnalysisContext context,
		IMethodSymbol method,
		INamedTypeSymbol networkEntityType,
		INamedTypeSymbol relayClientType,
		INamedTypeSymbol networkProfileType,
		INamedTypeSymbol rpcAttributeType,
		INamedTypeSymbol broadcastAttributeType) {
		if (method.MethodKind != MethodKind.Ordinary ||
		    method.IsStatic ||
		    method.DeclaredAccessibility != Accessibility.Public ||
		    method.ExplicitInterfaceImplementations.Length != 0 ||
		    (!NetworkAnalyzerHelpers.InheritsFrom(method.ContainingType, networkEntityType) &&
		     !SymbolEqualityComparer.Default.Equals(method.ContainingType, networkEntityType))) {
			return;
		}

		foreach ((IMethodSymbol messageMethod, INamedTypeSymbol attributeType) in GetExplicitMessageMethods(method.ContainingType, rpcAttributeType, broadcastAttributeType)) {
			if (!MatchesExplicitHandlerSignature(method, messageMethod, relayClientType, networkProfileType)) {
				continue;
			}

			context.ReportDiagnostic(Diagnostic.Create(
				NetworkMessageExplicitHandlerMustBeExplicitRule,
				method.Locations.FirstOrDefault(),
				method.Name,
				attributeType.Name,
				messageMethod.Name));
			return;
		}
	}

	private static IEnumerable<(IMethodSymbol Method, INamedTypeSymbol AttributeType)> GetExplicitMessageMethods(
		INamedTypeSymbol type,
		INamedTypeSymbol rpcAttributeType,
		INamedTypeSymbol broadcastAttributeType) {
		for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType) {
			foreach (IMethodSymbol method in current.GetMembers().OfType<IMethodSymbol>()) {
				foreach (AttributeData attribute in method.GetAttributes()) {
					if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, rpcAttributeType)) {
						if (IsExplicitReceiveMode(attribute)) {
							yield return (method, rpcAttributeType);
						}

						break;
					}

					if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, broadcastAttributeType)) {
						if (IsExplicitReceiveMode(attribute)) {
							yield return (method, broadcastAttributeType);
						}

						break;
					}
				}
			}
		}
	}

	private static bool IsExplicitReceiveMode(AttributeData attribute) {
		return attribute.ConstructorArguments.Length == 1 &&
		       attribute.ConstructorArguments[0].Value is int receiveMode &&
		       receiveMode == 1;
	}

	private static bool MatchesExplicitHandlerSignature(IMethodSymbol handler, IMethodSymbol messageMethod, INamedTypeSymbol relayClientType, INamedTypeSymbol networkProfileType) {
		if (handler.Name != messageMethod.Name ||
		    handler.TypeParameters.Length != 0 ||
		    handler.ReturnsVoid == false ||
		    handler.Parameters.Length != messageMethod.Parameters.Length + 2 ||
		    handler.Parameters.Any(static parameter => parameter.RefKind != RefKind.None)) {
			return false;
		}

		if (!SymbolEqualityComparer.Default.Equals(handler.Parameters[0].Type, relayClientType) ||
		    !SymbolEqualityComparer.Default.Equals(handler.Parameters[1].Type, networkProfileType)) {
			return false;
		}

		for (int i = 0; i < messageMethod.Parameters.Length; i++) {
			if (!SymbolEqualityComparer.Default.Equals(handler.Parameters[i + 2].Type, messageMethod.Parameters[i].Type)) {
				return false;
			}
		}

		return true;
	}

	private static bool IsSupportedParameterType(ITypeSymbol type, INamedTypeSymbol networkObjectType) {
		if (type is INamedTypeSymbol namedType &&
		    namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
		    namedType.TypeArguments.Length == 1) {
			return IsSupportedParameterType(namedType.TypeArguments[0], networkObjectType);
		}

		switch (type.SpecialType) {
			case SpecialType.System_Boolean:
			case SpecialType.System_Byte:
			case SpecialType.System_SByte:
			case SpecialType.System_Int16:
			case SpecialType.System_UInt16:
			case SpecialType.System_Int32:
			case SpecialType.System_UInt32:
			case SpecialType.System_Int64:
			case SpecialType.System_UInt64:
			case SpecialType.System_Single:
			case SpecialType.System_Double:
			case SpecialType.System_String:
				return true;
		}

		if (type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::System.Guid") {
			return true;
		}

		if (type is INamedTypeSymbol objectType &&
		    (SymbolEqualityComparer.Default.Equals(objectType, networkObjectType) || NetworkAnalyzerHelpers.InheritsFrom(objectType, networkObjectType))) {
			return true;
		}

		if (type.TypeKind == TypeKind.Struct && type is INamedTypeSymbol structType) {
			return NetworkAnalyzerHelpers.HasSupportedStructFieldShape(structType) && structType.GetMembers()
				.OfType<IFieldSymbol>()
				.Where(static field => !field.IsStatic && field.DeclaredAccessibility == Accessibility.Public)
				.All(field => IsSupportedParameterType(field.Type, networkObjectType) && !IsNetworkObjectType(field.Type, networkObjectType));
		}

		return false;
	}

	private static bool IsNetworkEntityType(ITypeSymbol type, INamedTypeSymbol networkEntityType) {
		ITypeSymbol effectiveType = UnwrapNullable(type);
		return effectiveType is INamedTypeSymbol namedType &&
		       (SymbolEqualityComparer.Default.Equals(namedType, networkEntityType) || NetworkAnalyzerHelpers.InheritsFrom(namedType, networkEntityType));
	}

	private static bool IsNetworkObjectType(ITypeSymbol type, INamedTypeSymbol networkObjectType) {
		ITypeSymbol effectiveType = UnwrapNullable(type);
		return effectiveType is INamedTypeSymbol namedType &&
		       (SymbolEqualityComparer.Default.Equals(namedType, networkObjectType) || NetworkAnalyzerHelpers.InheritsFrom(namedType, networkObjectType));
	}

	private static ITypeSymbol UnwrapNullable(ITypeSymbol type) {
		if (type is INamedTypeSymbol namedType &&
		    namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
		    namedType.TypeArguments.Length == 1) {
			return namedType.TypeArguments[0];
		}

		return type;
	}
}
