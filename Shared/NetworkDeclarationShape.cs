using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Cat.Network.CodeAnalysis;

// Compiled into both the analyzer and generator so rejected declaration shapes are not emitted.
internal static class NetworkDeclarationShape {
	public static string? GetUnsupportedModifier(ISymbol member) {
		if (member.IsStatic) return "static";
		if (member.IsAbstract) return "abstract";
		if (member.IsOverride) return "override";
		if (member.IsVirtual) return "virtual";
		if (member.IsSealed) return "sealed";
		if (member.IsExtern) return "extern";
		if (member is IPropertySymbol { IsRequired: true }) return "required";
		return null;
	}

	public static bool HasGetAndSet(IPropertySymbol property) {
		return property.GetMethod is not null && property.SetMethod is { IsInitOnly: false };
	}

	public static bool HasReadonlySerializedField(ITypeSymbol type) {
		return HasReadonlySerializedField(type, ImmutableHashSet<ITypeSymbol>.Empty.WithComparer(SymbolEqualityComparer.Default));
	}

	private static bool HasReadonlySerializedField(ITypeSymbol type, ImmutableHashSet<ITypeSymbol> visitedTypes) {
		if (type is INamedTypeSymbol nullableType &&
		    nullableType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
		    nullableType.TypeArguments.Length == 1) {
			type = nullableType.TypeArguments[0];
		}

		if (type is not INamedTypeSymbol { TypeKind: TypeKind.Struct } structType || visitedTypes.Contains(type)) {
			return false;
		}

		ImmutableHashSet<ITypeSymbol> nextVisitedTypes = visitedTypes.Add(type);
		return structType.GetMembers()
			.OfType<IFieldSymbol>()
			.Where(static field => !field.IsStatic && field.DeclaredAccessibility == Accessibility.Public)
			.Any(field => field.IsReadOnly || HasReadonlySerializedField(field.Type, nextVisitedTypes));
	}
}
