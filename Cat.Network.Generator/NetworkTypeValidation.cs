using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Cat.Network.Generator;

// Emission limitations are diagnosed by the generator so normal builds report each error once.
internal static class NetworkTypeValidation {
	private static readonly DiagnosticDescriptor GenericTypeRule = new(
		"CN0035", "Generic network types are not supported",
		"Network type '{0}' is generic or has a generic containing type; network types require a non-generic declaration and runtime type registration",
		"Usage", DiagnosticSeverity.Error, true);

	private static readonly DiagnosticDescriptor NestedTypeRule = new(
		"CN0036", "Nested network types are not supported",
		"Network type '{0}' is nested; declare it at namespace scope so its partial implementation and serializer can be generated",
		"Usage", DiagnosticSeverity.Error, true);

	private static readonly DiagnosticDescriptor FileLocalTypeRule = new(
		"CN0037", "File-local network types are not supported",
		"Network type '{0}' is file-local and cannot be extended from generated source files",
		"Usage", DiagnosticSeverity.Error, true);

	private static readonly DiagnosticDescriptor InaccessibleMemberTypeRule = new(
		"CN0038", "Network member types must be accessible to generated code",
		"Type '{0}' used by network member '{1}' is not accessible to generated code outside its declaring type and source file",
		"Usage", DiagnosticSeverity.Error, true);

	private static readonly DiagnosticDescriptor MessageApiTypeRule = new(
		"CN0039", "Message parameter types must be accessible through the receive API",
		"Type '{0}' used by message '{1}' must be public because the containing network type exposes a public receive interface",
		"Usage", DiagnosticSeverity.Error, true);

	public static ImmutableArray<Diagnostic> GetDiagnostics(INamedTypeSymbol type, Compilation compilation) {
		ImmutableArray<Diagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
		for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType) {
			if (!HasAttribute(current, "Cat.Network.NetworkObjectAttribute")) continue;

			// An unsupported base also prevents its derived serializer from using the generated base helpers.
			Location? location = type.Locations.FirstOrDefault();
			Diagnostic? scopeDiagnostic = GetScopeDiagnostic(current, location);
			if (scopeDiagnostic is not null) {
				diagnostics.Add(scopeDiagnostic);
				return diagnostics.ToImmutable();
			}

			foreach (ISymbol member in current.GetMembers()) {
				Location? memberLocation = SymbolEqualityComparer.Default.Equals(current, type)
					? member.Locations.FirstOrDefault() : location;
				if (member is IPropertySymbol property &&
				    (HasAttribute(property, "Cat.Network.NetworkPropertyAttribute") || HasAttribute(property, "Cat.Network.NetworkCollectionAttribute"))) {
					ValidateMemberType(property.Type, property, memberLocation, compilation, false, diagnostics);
				} else if (member is IMethodSymbol method &&
				           (HasAttribute(method, "Cat.Network.RPCAttribute") || HasAttribute(method, "Cat.Network.BroadcastAttribute"))) {
					foreach (IParameterSymbol parameter in method.Parameters) {
						ValidateMemberType(parameter.Type, method,
							SymbolEqualityComparer.Default.Equals(current, type) ? parameter.Locations.FirstOrDefault() : location,
							compilation, current.DeclaredAccessibility == Accessibility.Public, diagnostics);
					}
				}
			}
		}

		return diagnostics.ToImmutable();
	}

	private static Diagnostic? GetScopeDiagnostic(INamedTypeSymbol type, Location? location) {
		for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType) {
			if (current.Arity != 0) return Diagnostic.Create(GenericTypeRule, location, type.ToDisplayString());
		}
		if (type.ContainingType is not null) return Diagnostic.Create(NestedTypeRule, location, type.ToDisplayString());
		if (type.IsFileLocal) return Diagnostic.Create(FileLocalTypeRule, location, type.ToDisplayString());
		return null;
	}

	private static void ValidateMemberType(ITypeSymbol type, ISymbol member, Location? location, Compilation compilation,
		bool requiresPublicType, ImmutableArray<Diagnostic>.Builder diagnostics) {
		ITypeSymbol? inaccessibleType = FindInaccessibleType(type, compilation, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
		if (inaccessibleType is not null) {
			diagnostics.Add(Diagnostic.Create(InaccessibleMemberTypeRule, location, inaccessibleType.ToDisplayString(), member.ToDisplayString()));
			return;
		}

		if (requiresPublicType && !IsPublicType(type)) {
			diagnostics.Add(Diagnostic.Create(MessageApiTypeRule, location, type.ToDisplayString(), member.ToDisplayString()));
		}
	}

	private static ITypeSymbol? FindInaccessibleType(ITypeSymbol type, Compilation compilation, HashSet<ITypeSymbol> visited) {
		if (!visited.Add(type)) return null;
		if (type is IArrayTypeSymbol array) return FindInaccessibleType(array.ElementType, compilation, visited);
		if (type is not INamedTypeSymbol named) return null;
		if (named.IsFileLocal || !compilation.IsSymbolAccessibleWithin(named, compilation.Assembly)) return named;
		if (named.ContainingType is not null) {
			ITypeSymbol? inaccessibleContainer = FindInaccessibleType(named.ContainingType, compilation, visited);
			if (inaccessibleContainer is not null) return inaccessibleContainer;
		}

		foreach (ITypeSymbol argument in named.TypeArguments) {
			ITypeSymbol? inaccessibleArgument = FindInaccessibleType(argument, compilation, visited);
			if (inaccessibleArgument is not null) return inaccessibleArgument;
		}

		// Match the serializer's public instance-field traversal, including nested value types.
		if (named.TypeKind == TypeKind.Struct) {
			foreach (IFieldSymbol field in named.GetMembers().OfType<IFieldSymbol>().Where(static field => !field.IsStatic && field.DeclaredAccessibility == Accessibility.Public)) {
				ITypeSymbol? inaccessibleField = FindInaccessibleType(field.Type, compilation, visited);
				if (inaccessibleField is not null) return inaccessibleField;
			}
		}
		return null;
	}

	private static bool IsPublicType(ITypeSymbol type) {
		if (type is IArrayTypeSymbol array) return IsPublicType(array.ElementType);
		return type is not INamedTypeSymbol named ||
		       (named.DeclaredAccessibility == Accessibility.Public &&
		        (named.ContainingType is null || IsPublicType(named.ContainingType)) &&
		        named.TypeArguments.All(IsPublicType));
	}

	private static bool HasAttribute(ISymbol symbol, string metadataName) {
		return symbol.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == metadataName);
	}
}
