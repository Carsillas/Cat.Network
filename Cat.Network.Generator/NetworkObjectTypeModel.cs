using System;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Cat.Network.Generator;

internal sealed class NetworkObjectTypeModel : IEquatable<NetworkObjectTypeModel> {
	private const string NetworkPropertyAttributeMetadataName = "Cat.Network.NetworkPropertyAttribute";

	private static readonly SymbolDisplayFormat FullyQualifiedTypeFormat = new(
		SymbolDisplayGlobalNamespaceStyle.Included,
		SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
		SymbolDisplayGenericsOptions.IncludeTypeParameters,
		miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

	private NetworkObjectTypeModel(string @namespace, string typeName, string fullyQualifiedName, string baseTypeName, bool hasBaseProperties, string hintName, string serializerHintName, string accessibility, string serializerTypeName, string typeId, ImmutableArray<NetworkPropertyModel> declaredProperties, ImmutableArray<NetworkPropertyModel> properties) {
		Namespace = @namespace;
		TypeName = typeName;
		FullyQualifiedName = fullyQualifiedName;
		BaseTypeName = baseTypeName;
		HasBaseProperties = hasBaseProperties;
		HintName = hintName;
		SerializerHintName = serializerHintName;
		Accessibility = accessibility;
		SerializerTypeName = serializerTypeName;
		TypeId = typeId;
		DeclaredProperties = declaredProperties;
		Properties = properties;
	}

	public string Namespace { get; }

	public string TypeName { get; }

	public string FullyQualifiedName { get; }

	public string BaseTypeName { get; }

	public bool HasBaseProperties { get; }

	public string HintName { get; }

	public string SerializerHintName { get; }

	public string Accessibility { get; }

	public string SerializerTypeName { get; }

	public string TypeId { get; }

	public ImmutableArray<NetworkPropertyModel> DeclaredProperties { get; }

	public ImmutableArray<NetworkPropertyModel> Properties { get; }

	public static NetworkObjectTypeModel Create(INamedTypeSymbol type) {
		string @namespace = type.ContainingNamespace.IsGlobalNamespace ? string.Empty : type.ContainingNamespace.ToDisplayString();
		string typeName = type.Name;
		string fullyQualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		bool hasBaseProperties = type.BaseType is not null && type.BaseType.SpecialType != SpecialType.System_Object;
		string baseTypeName = hasBaseProperties
			? type.BaseType!.ToDisplayString(FullyQualifiedTypeFormat)
			: "global::Cat.Network.NetworkObject";
		string hintName = fullyQualifiedName
			.Replace("global::", string.Empty)
			.Replace(".", "_")
			.Replace("+", "_");
		string serializerTypeName = $"__CatNetwork_{typeName}_Serializer";
		string serializerHintName = $"{hintName}_Serializer";
		string typeId = CreateStableTypeId(type);
		ImmutableArray<NetworkPropertyModel> declaredProperties = type.GetMembers()
			.OfType<IPropertySymbol>()
			.Where(HasNetworkPropertyAttribute)
			.OrderBy(static property => property.Name, StringComparer.Ordinal)
			.Select(NetworkPropertyModel.Create)
			.ToImmutableArray();
		ImmutableArray<NetworkPropertyModel> properties = GetPropertiesInSerializationOrder(type);

		return new NetworkObjectTypeModel(@namespace, typeName, fullyQualifiedName, baseTypeName, hasBaseProperties, hintName, serializerHintName, GetAccessibility(type.DeclaredAccessibility), serializerTypeName, typeId, declaredProperties, properties);
	}

	public bool Equals(NetworkObjectTypeModel? other) {
		return other is not null &&
		       Namespace == other.Namespace &&
		       TypeName == other.TypeName &&
		       FullyQualifiedName == other.FullyQualifiedName &&
		       BaseTypeName == other.BaseTypeName &&
		       HasBaseProperties == other.HasBaseProperties &&
		       HintName == other.HintName &&
		       SerializerHintName == other.SerializerHintName &&
		       Accessibility == other.Accessibility &&
		       SerializerTypeName == other.SerializerTypeName &&
		       TypeId == other.TypeId &&
		       DeclaredProperties.SequenceEqual(other.DeclaredProperties) &&
		       Properties.SequenceEqual(other.Properties);
	}

	public override bool Equals(object? obj) {
		return obj is NetworkObjectTypeModel other && Equals(other);
	}

	public override int GetHashCode() {
		unchecked {
			int hashCode = Namespace.GetHashCode();
			hashCode = (hashCode * 397) ^ TypeName.GetHashCode();
			hashCode = (hashCode * 397) ^ FullyQualifiedName.GetHashCode();
			hashCode = (hashCode * 397) ^ BaseTypeName.GetHashCode();
			hashCode = (hashCode * 397) ^ HasBaseProperties.GetHashCode();
			hashCode = (hashCode * 397) ^ HintName.GetHashCode();
			hashCode = (hashCode * 397) ^ SerializerHintName.GetHashCode();
			hashCode = (hashCode * 397) ^ Accessibility.GetHashCode();
			hashCode = (hashCode * 397) ^ SerializerTypeName.GetHashCode();
			hashCode = (hashCode * 397) ^ TypeId.GetHashCode();
			foreach (NetworkPropertyModel property in DeclaredProperties) hashCode = (hashCode * 397) ^ property.GetHashCode();
			foreach (NetworkPropertyModel property in Properties) hashCode = (hashCode * 397) ^ property.GetHashCode();

			return hashCode;
		}
	}

	private static bool HasNetworkPropertyAttribute(IPropertySymbol property) {
		return property.GetAttributes().Any(attribute =>
			attribute.AttributeClass?.ToDisplayString() == NetworkPropertyAttributeMetadataName);
	}

	private static ImmutableArray<NetworkPropertyModel> GetPropertiesInSerializationOrder(INamedTypeSymbol type) {
		ImmutableArray<INamedTypeSymbol> inheritanceChain = GetInheritanceChain(type);
		return inheritanceChain
			.SelectMany(currentType => currentType
				.GetMembers()
				.OfType<IPropertySymbol>()
				.Where(HasNetworkPropertyAttribute)
				.OrderBy(static property => property.Name, StringComparer.Ordinal)
				.Select(NetworkPropertyModel.Create))
			.ToImmutableArray();
	}

	private static ImmutableArray<INamedTypeSymbol> GetInheritanceChain(INamedTypeSymbol type) {
		ImmutableStack<INamedTypeSymbol> stack = ImmutableStack<INamedTypeSymbol>.Empty;

		for (INamedTypeSymbol? current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType) {
			stack = stack.Push(current);
		}

		ImmutableArray<INamedTypeSymbol>.Builder builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
		while (!stack.IsEmpty) {
			builder.Add(stack.Peek());
			stack = stack.Pop();
		}

		return builder.ToImmutable();
	}

	private static string CreateStableTypeId(INamedTypeSymbol type) {
		string assemblyQualifiedName = $"{type.ToDisplayString(FullyQualifiedTypeFormat)}, {type.ContainingAssembly.Name}";
		byte[] bytes = Encoding.UTF8.GetBytes(assemblyQualifiedName);
		byte[] hash;
		using (var sha256 = SHA256.Create()) {
			hash = sha256.ComputeHash(bytes);
		}

		byte[] guidBytes = new byte[16];
		Array.Copy(hash, guidBytes, guidBytes.Length);
		return new Guid(guidBytes).ToString();
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
