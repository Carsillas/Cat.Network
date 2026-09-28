using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Cat.Network.Generator;

internal sealed class NetworkStructFieldModel : IEquatable<NetworkStructFieldModel> {
	public static bool HasSupportedStructFieldShape(INamedTypeSymbol type) {
		// Built-in value types need explicit codecs; their private fields may be omitted from metadata symbols.
		if (type.SpecialType != SpecialType.None) {
			return false;
		}

		IFieldSymbol[] instanceFields = type.GetMembers().OfType<IFieldSymbol>()
			.Where(static field => !field.IsStatic).ToArray();
		// Keep the same public-field/empty-struct rule as the analyzer and runtime codecs.
		return instanceFields.Length == 0 || instanceFields.Any(static field => field.DeclaredAccessibility == Accessibility.Public);
	}

	private static readonly SymbolDisplayFormat FullyQualifiedTypeFormat = new(
		SymbolDisplayGlobalNamespaceStyle.Included,
		SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		SymbolDisplayGenericsOptions.IncludeTypeParameters,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

	private static readonly SymbolDisplayFormat FullyQualifiedNonNullableTypeFormat = new(
		SymbolDisplayGlobalNamespaceStyle.Included,
		SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		SymbolDisplayGenericsOptions.IncludeTypeParameters);

	private NetworkStructFieldModel(string typeName, string runtimeTypeName, string name, NetworkPropertySerializationKind serializationKind, bool isNullableValueType, ImmutableArray<NetworkStructFieldModel> structFields) {
		TypeName = typeName;
		RuntimeTypeName = runtimeTypeName;
		Name = name;
		SerializationKind = serializationKind;
		IsNullableValueType = isNullableValueType;
		StructFields = structFields;
	}

	public string TypeName { get; }

	public string RuntimeTypeName { get; }

	public string Name { get; }

	public NetworkPropertySerializationKind SerializationKind { get; }

	public bool IsNullableValueType { get; }

	public ImmutableArray<NetworkStructFieldModel> StructFields { get; }

	public static NetworkStructFieldModel Create(IFieldSymbol field) {
		return Create(field, ImmutableHashSet<ITypeSymbol>.Empty.WithComparer(SymbolEqualityComparer.Default));
	}

	private static NetworkStructFieldModel Create(IFieldSymbol field, ImmutableHashSet<ITypeSymbol> visitedTypes) {
		(ITypeSymbol effectiveType, bool isNullableValueType) = GetEffectiveType(field.Type);
		(NetworkPropertySerializationKind serializationKind, ImmutableArray<NetworkStructFieldModel> structFields) = GetSerializationMetadata(effectiveType, visitedTypes);

		return new NetworkStructFieldModel(
			field.Type.ToDisplayString(FullyQualifiedTypeFormat),
			effectiveType.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(FullyQualifiedNonNullableTypeFormat),
			field.Name,
			serializationKind,
			isNullableValueType,
			structFields);
	}

	public bool Equals(NetworkStructFieldModel? other) {
		return other is not null &&
		       TypeName == other.TypeName &&
		       RuntimeTypeName == other.RuntimeTypeName &&
		       Name == other.Name &&
		       SerializationKind == other.SerializationKind &&
		       IsNullableValueType == other.IsNullableValueType &&
		       StructFields.SequenceEqual(other.StructFields);
	}

	public override bool Equals(object? obj) {
		return obj is NetworkStructFieldModel other && Equals(other);
	}

	public override int GetHashCode() {
		unchecked {
			int hashCode = TypeName.GetHashCode();
			hashCode = (hashCode * 397) ^ RuntimeTypeName.GetHashCode();
			hashCode = (hashCode * 397) ^ Name.GetHashCode();
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

	private static (NetworkPropertySerializationKind SerializationKind, ImmutableArray<NetworkStructFieldModel> StructFields) GetSerializationMetadata(ITypeSymbol type, ImmutableHashSet<ITypeSymbol> visitedTypes) {
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

		if (type.TypeKind == TypeKind.Struct && type is INamedTypeSymbol structType) {
			if (!HasSupportedStructFieldShape(structType) || visitedTypes.Contains(structType)) {
				return (NetworkPropertySerializationKind.Unsupported, ImmutableArray<NetworkStructFieldModel>.Empty);
			}

			ImmutableHashSet<ITypeSymbol> nextVisitedTypes = visitedTypes.Add(structType);
			ImmutableArray<NetworkStructFieldModel> structFields = structType.GetMembers()
				.OfType<IFieldSymbol>()
				.Where(static field => !field.IsStatic && field.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Public)
				.OrderBy(static field => field.Name, StringComparer.Ordinal)
				.Select(field => Create(field, nextVisitedTypes))
				.ToImmutableArray();

			if (structFields.All(static field => field.SerializationKind != NetworkPropertySerializationKind.Unsupported &&
			                                    field.SerializationKind != NetworkPropertySerializationKind.NetworkObject)) {
				return (NetworkPropertySerializationKind.Struct, structFields);
			}
		}

		return (NetworkPropertySerializationKind.Unsupported, ImmutableArray<NetworkStructFieldModel>.Empty);
	}
}
