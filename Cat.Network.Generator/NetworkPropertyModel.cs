using System;
using Microsoft.CodeAnalysis;

namespace Cat.Network.Generator;

internal sealed class NetworkPropertyModel : IEquatable<NetworkPropertyModel> {
	private static readonly SymbolDisplayFormat FullyQualifiedTypeFormat = new(
		SymbolDisplayGlobalNamespaceStyle.Included,
		SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		SymbolDisplayGenericsOptions.IncludeTypeParameters,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

	private NetworkPropertyModel(string typeName, string name, string accessibility, string getterAccessibility, string setterAccessibility) {
		TypeName = typeName;
		Name = name;
		Accessibility = accessibility;
		GetterAccessibility = getterAccessibility;
		SetterAccessibility = setterAccessibility;
	}

	public string TypeName { get; }

	public string Name { get; }

	public string Accessibility { get; }

	public string GetterAccessibility { get; }

	public string SetterAccessibility { get; }

	public static NetworkPropertyModel Create(IPropertySymbol property) {
		return new NetworkPropertyModel(
			property.Type.ToDisplayString(FullyQualifiedTypeFormat),
			property.Name,
			GetAccessibility(property.DeclaredAccessibility),
			GetAccessorAccessibility(property, property.GetMethod),
			GetAccessorAccessibility(property, property.SetMethod));
	}

	public bool Equals(NetworkPropertyModel? other) {
		return other is not null &&
		       TypeName == other.TypeName &&
		       Name == other.Name &&
		       Accessibility == other.Accessibility &&
		       GetterAccessibility == other.GetterAccessibility &&
		       SetterAccessibility == other.SetterAccessibility;
	}

	public override bool Equals(object? obj) {
		return obj is NetworkPropertyModel other && Equals(other);
	}

	public override int GetHashCode() {
		unchecked {
			int hashCode = TypeName.GetHashCode();
			hashCode = (hashCode * 397) ^ Name.GetHashCode();
			hashCode = (hashCode * 397) ^ Accessibility.GetHashCode();
			hashCode = (hashCode * 397) ^ GetterAccessibility.GetHashCode();
			hashCode = (hashCode * 397) ^ SetterAccessibility.GetHashCode();
			return hashCode;
		}
	}

	private static string GetAccessorAccessibility(IPropertySymbol property, IMethodSymbol? accessor) {
		if (accessor is null || accessor.DeclaredAccessibility == property.DeclaredAccessibility) {
			return string.Empty;
		}

		return GetAccessibility(accessor.DeclaredAccessibility) + " ";
	}

	private static string GetAccessibility(Accessibility accessibility) {
		switch (accessibility) {
			case Microsoft.CodeAnalysis.Accessibility.Public:
				return "public";
			case Microsoft.CodeAnalysis.Accessibility.Internal:
				return "internal";
			case Microsoft.CodeAnalysis.Accessibility.Protected:
				return "protected";
			case Microsoft.CodeAnalysis.Accessibility.Private:
				return "private";
			case Microsoft.CodeAnalysis.Accessibility.ProtectedAndInternal:
				return "private protected";
			case Microsoft.CodeAnalysis.Accessibility.ProtectedOrInternal:
				return "protected internal";
			default:
				return "private";
		}
	}
}
