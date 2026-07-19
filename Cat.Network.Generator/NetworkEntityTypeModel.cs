using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Cat.Network.Generator;

internal sealed class NetworkEntityTypeModel : IEquatable<NetworkEntityTypeModel> {
	private const string NetworkPropertyAttributeMetadataName = "Cat.Network.NetworkPropertyAttribute";

	private static readonly SymbolDisplayFormat FullyQualifiedTypeFormat = new(
		SymbolDisplayGlobalNamespaceStyle.Included,
		SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		SymbolDisplayGenericsOptions.IncludeTypeParameters,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

	private NetworkEntityTypeModel(string @namespace, string typeName, string fullyQualifiedName, string baseTypeName, string hintName, string accessibility, ImmutableArray<NetworkPropertyModel> properties) {
		Namespace = @namespace;
		TypeName = typeName;
		FullyQualifiedName = fullyQualifiedName;
		BaseTypeName = baseTypeName;
		HintName = hintName;
		Accessibility = accessibility;
		Properties = properties;
	}

	public string Namespace { get; }

	public string TypeName { get; }

	public string FullyQualifiedName { get; }

	public string BaseTypeName { get; }

	public string HintName { get; }

	public string Accessibility { get; }

	public ImmutableArray<NetworkPropertyModel> Properties { get; }

	public static NetworkEntityTypeModel Create(INamedTypeSymbol type) {
		string @namespace = type.ContainingNamespace.IsGlobalNamespace ? string.Empty : type.ContainingNamespace.ToDisplayString();
		string typeName = type.Name;
		string fullyQualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		string baseTypeName = type.BaseType?.ToDisplayString(FullyQualifiedTypeFormat) ?? "global::Cat.Network.NetworkEntity";
		string hintName = fullyQualifiedName
			.Replace("global::", string.Empty)
			.Replace(".", "_")
			.Replace("+", "_");
		ImmutableArray<NetworkPropertyModel> properties = type.GetMembers()
			.OfType<IPropertySymbol>()
			.Where(HasNetworkPropertyAttribute)
			.Select(NetworkPropertyModel.Create)
			.ToImmutableArray();

		return new NetworkEntityTypeModel(@namespace, typeName, fullyQualifiedName, baseTypeName, hintName, GetAccessibility(type.DeclaredAccessibility), properties);
	}

	public bool Equals(NetworkEntityTypeModel? other) {
		return other is not null &&
		       Namespace == other.Namespace &&
		       TypeName == other.TypeName &&
		       FullyQualifiedName == other.FullyQualifiedName &&
		       BaseTypeName == other.BaseTypeName &&
		       HintName == other.HintName &&
		       Accessibility == other.Accessibility &&
		       Properties.SequenceEqual(other.Properties);
	}

	public override bool Equals(object? obj) {
		return obj is NetworkEntityTypeModel other && Equals(other);
	}

	public override int GetHashCode() {
		unchecked {
			int hashCode = Namespace.GetHashCode();
			hashCode = (hashCode * 397) ^ TypeName.GetHashCode();
			hashCode = (hashCode * 397) ^ FullyQualifiedName.GetHashCode();
			hashCode = (hashCode * 397) ^ BaseTypeName.GetHashCode();
			hashCode = (hashCode * 397) ^ HintName.GetHashCode();
			hashCode = (hashCode * 397) ^ Accessibility.GetHashCode();
			foreach (NetworkPropertyModel property in Properties) hashCode = (hashCode * 397) ^ property.GetHashCode();

			return hashCode;
		}
	}

	private static bool HasNetworkPropertyAttribute(IPropertySymbol property) {
		return property.GetAttributes().Any(attribute =>
			attribute.AttributeClass?.ToDisplayString() == NetworkPropertyAttributeMetadataName);
	}

	private static string GetAccessibility(Accessibility accessibility) {
		switch (accessibility) {
			case Microsoft.CodeAnalysis.Accessibility.Public:
				return "public";
			case Microsoft.CodeAnalysis.Accessibility.Internal:
				return "internal";
			default:
				return "private";
		}
	}
}