using System.Collections.Immutable;
using System;
using System.Linq;
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

	private NetworkPropertyModel(string typeName, string runtimeTypeName, string declaringTypeName, string name, string accessibility, string getterAccessibility, string setterAccessibility, int propertyIndex, NetworkPropertySerializationKind serializationKind, bool isNullableValueType, ImmutableArray<NetworkStructFieldModel> structFields) {
		TypeName = typeName;
		RuntimeTypeName = runtimeTypeName;
		DeclaringTypeName = declaringTypeName;
		Name = name;
		Accessibility = accessibility;
		GetterAccessibility = getterAccessibility;
		SetterAccessibility = setterAccessibility;
		PropertyIndex = propertyIndex;
		SerializationKind = serializationKind;
		IsNullableValueType = isNullableValueType;
		StructFields = structFields;
	}

	public string TypeName { get; }

	public string RuntimeTypeName { get; }

	public string DeclaringTypeName { get; }

	public string Name { get; }

	public string Accessibility { get; }

	public string GetterAccessibility { get; }

	public string SetterAccessibility { get; }

	public int PropertyIndex { get; }

	public NetworkPropertySerializationKind SerializationKind { get; }

	public bool IsNullableValueType { get; }

	public ImmutableArray<NetworkStructFieldModel> StructFields { get; }

	public static NetworkPropertyModel Create(IPropertySymbol property, int propertyIndex) {
		(ITypeSymbol effectiveType, bool isNullableValueType) = GetEffectiveType(property.Type);
		(NetworkPropertySerializationKind serializationKind, ImmutableArray<NetworkStructFieldModel> structFields) = GetSerializationMetadata(effectiveType);

		return new NetworkPropertyModel(
			property.Type.ToDisplayString(FullyQualifiedTypeFormat),
			effectiveType.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(FullyQualifiedNonNullableTypeFormat),
			property.ContainingType.ToDisplayString(FullyQualifiedTypeFormat),
			property.Name,
			GetAccessibility(property.DeclaredAccessibility),
			GetAccessorAccessibility(property, property.GetMethod),
			GetAccessorAccessibility(property, property.SetMethod),
			propertyIndex,
			serializationKind,
			isNullableValueType,
			structFields);
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
		       PropertyIndex == other.PropertyIndex &&
		       SerializationKind == other.SerializationKind &&
		       IsNullableValueType == other.IsNullableValueType &&
		       StructFields.SequenceEqual(other.StructFields);
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
			hashCode = (hashCode * 397) ^ PropertyIndex;
			hashCode = (hashCode * 397) ^ (int)SerializationKind;
			hashCode = (hashCode * 397) ^ IsNullableValueType.GetHashCode();
			foreach (NetworkStructFieldModel field in StructFields) hashCode = (hashCode * 397) ^ field.GetHashCode();
			return hashCode;
		}
	}

	private static (ITypeSymbol EffectiveType, bool IsNullableValueType) GetEffectiveType(ITypeSymbol type) {
		if (type is INamedTypeSymbol namedType &&
		    namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
		    namedType.TypeArguments.Length == 1) {
			return (namedType.TypeArguments[0], true);
		}

		return (type, false);
	}

	private static (NetworkPropertySerializationKind SerializationKind, ImmutableArray<NetworkStructFieldModel> StructFields) GetSerializationMetadata(ITypeSymbol type) {
		switch (type.SpecialType) {
			case SpecialType.System_Boolean:
				return (NetworkPropertySerializationKind.Boolean, ImmutableArray<NetworkStructFieldModel>.Empty);
			case SpecialType.System_Byte:
				return (NetworkPropertySerializationKind.Byte, ImmutableArray<NetworkStructFieldModel>.Empty);
			case SpecialType.System_SByte:
				return (NetworkPropertySerializationKind.SByte, ImmutableArray<NetworkStructFieldModel>.Empty);
			case SpecialType.System_Int16:
				return (NetworkPropertySerializationKind.Int16, ImmutableArray<NetworkStructFieldModel>.Empty);
			case SpecialType.System_UInt16:
				return (NetworkPropertySerializationKind.UInt16, ImmutableArray<NetworkStructFieldModel>.Empty);
			case SpecialType.System_Int32:
				return (NetworkPropertySerializationKind.Int32, ImmutableArray<NetworkStructFieldModel>.Empty);
			case SpecialType.System_UInt32:
				return (NetworkPropertySerializationKind.UInt32, ImmutableArray<NetworkStructFieldModel>.Empty);
			case SpecialType.System_Int64:
				return (NetworkPropertySerializationKind.Int64, ImmutableArray<NetworkStructFieldModel>.Empty);
			case SpecialType.System_UInt64:
				return (NetworkPropertySerializationKind.UInt64, ImmutableArray<NetworkStructFieldModel>.Empty);
			case SpecialType.System_Single:
				return (NetworkPropertySerializationKind.Single, ImmutableArray<NetworkStructFieldModel>.Empty);
			case SpecialType.System_Double:
				return (NetworkPropertySerializationKind.Double, ImmutableArray<NetworkStructFieldModel>.Empty);
			case SpecialType.System_String:
				return (NetworkPropertySerializationKind.String, ImmutableArray<NetworkStructFieldModel>.Empty);
		}

		if (type.ToDisplayString(FullyQualifiedTypeFormat) == "global::System.Guid") {
			return (NetworkPropertySerializationKind.Guid, ImmutableArray<NetworkStructFieldModel>.Empty);
		}

		for (ITypeSymbol? current = type; current is not null; current = current.BaseType) {
			if (current.ToDisplayString(FullyQualifiedTypeFormat) == "global::Cat.Network.NetworkObject") {
				return (NetworkPropertySerializationKind.NetworkObject, ImmutableArray<NetworkStructFieldModel>.Empty);
			}
		}

		if (type.TypeKind == TypeKind.Struct && type is INamedTypeSymbol structType && NetworkStructFieldModel.HasSupportedStructFieldShape(structType)) {
			ImmutableArray<NetworkStructFieldModel> structFields = structType.GetMembers()
				.OfType<IFieldSymbol>()
				.Where(static field => !field.IsStatic && field.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public)
				.OrderBy(static field => field.Name, StringComparer.Ordinal)
				.Select(NetworkStructFieldModel.Create)
				.ToImmutableArray();

			if (structFields.All(static field => field.SerializationKind != NetworkPropertySerializationKind.Unsupported &&
			                                    field.SerializationKind != NetworkPropertySerializationKind.NetworkObject)) {
				return (NetworkPropertySerializationKind.Struct, structFields);
			}
		}

		return (NetworkPropertySerializationKind.Unsupported, ImmutableArray<NetworkStructFieldModel>.Empty);
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
