using System;
using Microsoft.CodeAnalysis;

namespace Cat.Network.Generator;

internal sealed class NetworkPropertyModel : IEquatable<NetworkPropertyModel> {
	private static readonly SymbolDisplayFormat FullyQualifiedTypeFormat = new(
		SymbolDisplayGlobalNamespaceStyle.Included,
		SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		SymbolDisplayGenericsOptions.IncludeTypeParameters,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

	private static readonly SymbolDisplayFormat FullyQualifiedNonNullableTypeFormat = new(
		SymbolDisplayGlobalNamespaceStyle.Included,
		SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		SymbolDisplayGenericsOptions.IncludeTypeParameters);

	private NetworkPropertyModel(string typeName, string runtimeTypeName, string declaringTypeName, string name, string accessibility, string getterAccessibility, string setterAccessibility, NetworkPropertySerializationKind serializationKind) {
		TypeName = typeName;
		RuntimeTypeName = runtimeTypeName;
		DeclaringTypeName = declaringTypeName;
		Name = name;
		Accessibility = accessibility;
		GetterAccessibility = getterAccessibility;
		SetterAccessibility = setterAccessibility;
		SerializationKind = serializationKind;
	}

	public string TypeName { get; }

	public string RuntimeTypeName { get; }

	public string DeclaringTypeName { get; }

	public string Name { get; }

	public string Accessibility { get; }

	public string GetterAccessibility { get; }

	public string SetterAccessibility { get; }

	public NetworkPropertySerializationKind SerializationKind { get; }

	public static NetworkPropertyModel Create(IPropertySymbol property) {
		return new NetworkPropertyModel(
			property.Type.ToDisplayString(FullyQualifiedTypeFormat),
			property.Type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(FullyQualifiedNonNullableTypeFormat),
			property.ContainingType.ToDisplayString(FullyQualifiedTypeFormat),
			property.Name,
			GetAccessibility(property.DeclaredAccessibility),
			GetAccessorAccessibility(property, property.GetMethod),
			GetAccessorAccessibility(property, property.SetMethod),
			GetSerializationKind(property.Type));
	}

	public bool Equals(NetworkPropertyModel? other) {
		return other is not null &&
		       TypeName == other.TypeName &&
		       RuntimeTypeName == other.RuntimeTypeName &&
		       DeclaringTypeName == other.DeclaringTypeName &&
		       Name == other.Name &&
		       Accessibility == other.Accessibility &&
		       GetterAccessibility == other.GetterAccessibility &&
		       SetterAccessibility == other.SetterAccessibility &&
		       SerializationKind == other.SerializationKind;
	}

	public override bool Equals(object? obj) {
		return obj is NetworkPropertyModel other && Equals(other);
	}

	public override int GetHashCode() {
		unchecked {
			int hashCode = TypeName.GetHashCode();
			hashCode = (hashCode * 397) ^ RuntimeTypeName.GetHashCode();
			hashCode = (hashCode * 397) ^ DeclaringTypeName.GetHashCode();
			hashCode = (hashCode * 397) ^ Name.GetHashCode();
			hashCode = (hashCode * 397) ^ Accessibility.GetHashCode();
			hashCode = (hashCode * 397) ^ GetterAccessibility.GetHashCode();
			hashCode = (hashCode * 397) ^ SetterAccessibility.GetHashCode();
			hashCode = (hashCode * 397) ^ (int)SerializationKind;
			return hashCode;
		}
	}

	private static NetworkPropertySerializationKind GetSerializationKind(ITypeSymbol type) {
		switch (type.SpecialType) {
			case SpecialType.System_Boolean:
				return NetworkPropertySerializationKind.Boolean;
			case SpecialType.System_Byte:
				return NetworkPropertySerializationKind.Byte;
			case SpecialType.System_SByte:
				return NetworkPropertySerializationKind.SByte;
			case SpecialType.System_Int16:
				return NetworkPropertySerializationKind.Int16;
			case SpecialType.System_UInt16:
				return NetworkPropertySerializationKind.UInt16;
			case SpecialType.System_Int32:
				return NetworkPropertySerializationKind.Int32;
			case SpecialType.System_UInt32:
				return NetworkPropertySerializationKind.UInt32;
			case SpecialType.System_Int64:
				return NetworkPropertySerializationKind.Int64;
			case SpecialType.System_UInt64:
				return NetworkPropertySerializationKind.UInt64;
			case SpecialType.System_Single:
				return NetworkPropertySerializationKind.Single;
			case SpecialType.System_Double:
				return NetworkPropertySerializationKind.Double;
			case SpecialType.System_String:
				return NetworkPropertySerializationKind.String;
		}

		if (type.ToDisplayString(FullyQualifiedTypeFormat) == "global::System.Guid") {
			return NetworkPropertySerializationKind.Guid;
		}

		for (ITypeSymbol? current = type; current is not null; current = current.BaseType) {
			if (current.ToDisplayString(FullyQualifiedTypeFormat) == "global::Cat.Network.NetworkObject") {
				return NetworkPropertySerializationKind.NetworkObject;
			}
		}

		return NetworkPropertySerializationKind.Unsupported;
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
