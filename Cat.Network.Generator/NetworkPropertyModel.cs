using System;
using Microsoft.CodeAnalysis;

namespace Cat.Network.Generator;

internal sealed class NetworkPropertyModel : IEquatable<NetworkPropertyModel> {
	private static readonly SymbolDisplayFormat FullyQualifiedTypeFormat = new(
		SymbolDisplayGlobalNamespaceStyle.Included,
		SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		SymbolDisplayGenericsOptions.IncludeTypeParameters,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

	private NetworkPropertyModel(string typeName, string name, bool hasGetter, bool hasSetter) {
		TypeName = typeName;
		Name = name;
		HasGetter = hasGetter;
		HasSetter = hasSetter;
	}

	public string TypeName { get; }

	public string Name { get; }

	public bool HasGetter { get; }

	public bool HasSetter { get; }

	public static NetworkPropertyModel Create(IPropertySymbol property) {
		return new NetworkPropertyModel(
			property.Type.ToDisplayString(FullyQualifiedTypeFormat),
			property.Name,
			property.GetMethod is not null,
			property.SetMethod is not null);
	}

	public bool Equals(NetworkPropertyModel? other) {
		return other is not null &&
		       TypeName == other.TypeName &&
		       Name == other.Name &&
		       HasGetter == other.HasGetter &&
		       HasSetter == other.HasSetter;
	}

	public override bool Equals(object? obj) {
		return obj is NetworkPropertyModel other && Equals(other);
	}

	public override int GetHashCode() {
		unchecked {
			int hashCode = TypeName.GetHashCode();
			hashCode = (hashCode * 397) ^ Name.GetHashCode();
			hashCode = (hashCode * 397) ^ HasGetter.GetHashCode();
			hashCode = (hashCode * 397) ^ HasSetter.GetHashCode();
			return hashCode;
		}
	}
}
