using System;
using Microsoft.CodeAnalysis;

namespace Cat.Network.Generator;

internal sealed class NetworkCollectionModel : IEquatable<NetworkCollectionModel> {
	private static readonly SymbolDisplayFormat FullyQualifiedTypeFormat = new(
		SymbolDisplayGlobalNamespaceStyle.Included,
		SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		SymbolDisplayGenericsOptions.IncludeTypeParameters,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

	private static readonly SymbolDisplayFormat FullyQualifiedNonNullableTypeFormat = new(
		SymbolDisplayGlobalNamespaceStyle.Included,
		SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		SymbolDisplayGenericsOptions.IncludeTypeParameters);

	private NetworkCollectionModel(string typeName, string itemTypeName, string runtimeItemTypeName, string name, string accessibility, string getterAccessibility, int propertyIndex, bool isNetworkObjectItem, string backingFieldName) {
		TypeName = typeName;
		ItemTypeName = itemTypeName;
		RuntimeItemTypeName = runtimeItemTypeName;
		Name = name;
		Accessibility = accessibility;
		GetterAccessibility = getterAccessibility;
		PropertyIndex = propertyIndex;
		IsNetworkObjectItem = isNetworkObjectItem;
		BackingFieldName = backingFieldName;
	}

	public string TypeName { get; }

	public string ItemTypeName { get; }

	public string RuntimeItemTypeName { get; }

	public string Name { get; }

	public string Accessibility { get; }

	public string GetterAccessibility { get; }

	public int PropertyIndex { get; }

	public bool IsNetworkObjectItem { get; }

	public string BackingFieldName { get; }

	public static NetworkCollectionModel Create(IPropertySymbol property, int propertyIndex) {
		INamedTypeSymbol propertyType = (INamedTypeSymbol)property.Type;
		ITypeSymbol itemType = propertyType.TypeArguments[0];
		bool isNetworkObjectItem = InheritsFromNetworkObject(itemType);
		string itemTypeName = itemType.ToDisplayString(FullyQualifiedTypeFormat);
		string runtimeItemTypeName = itemType.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(FullyQualifiedNonNullableTypeFormat);

		return new NetworkCollectionModel(
			property.Type.ToDisplayString(FullyQualifiedTypeFormat),
			itemTypeName,
			runtimeItemTypeName,
			property.Name,
			GetAccessibility(property.DeclaredAccessibility),
			GetAccessorAccessibility(property, property.GetMethod),
			propertyIndex,
			isNetworkObjectItem,
			$"__networkCollection_{property.Name}");
	}

	public bool Equals(NetworkCollectionModel? other) {
		return other is not null &&
		       TypeName == other.TypeName &&
		       ItemTypeName == other.ItemTypeName &&
		       RuntimeItemTypeName == other.RuntimeItemTypeName &&
		       Name == other.Name &&
		       Accessibility == other.Accessibility &&
		       GetterAccessibility == other.GetterAccessibility &&
		       PropertyIndex == other.PropertyIndex &&
		       IsNetworkObjectItem == other.IsNetworkObjectItem &&
		       BackingFieldName == other.BackingFieldName;
	}

	public override bool Equals(object? obj) {
		return obj is NetworkCollectionModel other && Equals(other);
	}

	public override int GetHashCode() {
		unchecked {
			int hashCode = TypeName.GetHashCode();
			hashCode = (hashCode * 397) ^ ItemTypeName.GetHashCode();
			hashCode = (hashCode * 397) ^ RuntimeItemTypeName.GetHashCode();
			hashCode = (hashCode * 397) ^ Name.GetHashCode();
			hashCode = (hashCode * 397) ^ Accessibility.GetHashCode();
			hashCode = (hashCode * 397) ^ GetterAccessibility.GetHashCode();
			hashCode = (hashCode * 397) ^ PropertyIndex;
			hashCode = (hashCode * 397) ^ IsNetworkObjectItem.GetHashCode();
			hashCode = (hashCode * 397) ^ BackingFieldName.GetHashCode();
			return hashCode;
		}
	}

	private static bool InheritsFromNetworkObject(ITypeSymbol type) {
		for (ITypeSymbol? current = type; current is not null; current = current.BaseType) {
			if (current.ToDisplayString(FullyQualifiedNonNullableTypeFormat) == "global::Cat.Network.NetworkObject") {
				return true;
			}
		}

		return false;
	}

	private static string GetAccessorAccessibility(IPropertySymbol property, IMethodSymbol? accessor) {
		if (accessor is null || accessor.DeclaredAccessibility == property.DeclaredAccessibility) {
			return string.Empty;
		}

		return GetAccessibility(accessor.DeclaredAccessibility) + " ";
	}

	private static string GetAccessibility(Accessibility accessibility) {
		return accessibility switch {
			Microsoft.CodeAnalysis.Accessibility.Public => "public",
			Microsoft.CodeAnalysis.Accessibility.Internal => "internal",
			Microsoft.CodeAnalysis.Accessibility.Protected => "protected",
			Microsoft.CodeAnalysis.Accessibility.Private => "private",
			Microsoft.CodeAnalysis.Accessibility.ProtectedAndInternal => "private protected",
			Microsoft.CodeAnalysis.Accessibility.ProtectedOrInternal => "protected internal",
			_ => "private"
		};
	}
}
