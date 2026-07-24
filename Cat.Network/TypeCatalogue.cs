using System.Diagnostics.CodeAnalysis;

namespace Cat.Network;

public class TypeCatalogue {
	
	private Dictionary<Guid, Type> Types { get; } = [];
	private Dictionary<Type, Type> Serializers { get; } = [];

	public TypeCatalogue Clone() {
		TypeCatalogue clone = new TypeCatalogue();
		foreach (KeyValuePair<Guid, Type> entry in Types) {
			clone.Types.Add(entry.Key, entry.Value);
		}
		foreach (KeyValuePair<Type, Type> entry in Serializers) {
			clone.Serializers.Add(entry.Key, entry.Value);
		}

		return clone;
	}

	public void Register(Type type) {
		ArgumentNullException.ThrowIfNull(type);
		if (!typeof(NetworkObject).IsAssignableFrom(type)) {
			throw new InvalidOperationException($"Type '{type.FullName}' does not inherit from {nameof(NetworkObject)}.");
		}

		NetworkObjectTypeId? typeId = type.GetCustomAttributes(typeof(NetworkObjectTypeId), inherit: false)
			.OfType<NetworkObjectTypeId>()
			.SingleOrDefault();
		
		if (typeId is null) {
			throw new InvalidOperationException($"Type '{type.FullName}' is missing {nameof(NetworkObjectTypeId)}.");
		}

		NetworkObjectSerializerAttribute? serializerAttribute = type.GetCustomAttributes(typeof(NetworkObjectSerializerAttribute), inherit: false)
			.OfType<NetworkObjectSerializerAttribute>()
			.SingleOrDefault();
		if (serializerAttribute is null) {
			throw new InvalidOperationException($"Type '{type.FullName}' is missing {nameof(NetworkObjectSerializerAttribute)}.");
		}

		Type serializerType = serializerAttribute.SerializerType;
		if (Types.TryGetValue(typeId.Id, out Type? existingType) && existingType != type) {
			throw new InvalidOperationException($"Type id '{typeId.Id}' is already registered to '{existingType.FullName}'.");
		}
		if (Serializers.TryGetValue(type, out Type? existingSerializerType) && existingSerializerType != serializerType) {
			throw new InvalidOperationException($"Type '{type.FullName}' is already registered to serializer '{existingSerializerType.FullName}'.");
		}

		Types[typeId.Id] = type;
		Serializers[type] = serializerType;
	}
	
	public bool TryFindType(Guid id, [NotNullWhen(true)] out Type? type) {
		return Types.TryGetValue(id, out type);
	}

	public bool TryFindSerializer(Type type, [NotNullWhen(true)] out Type? serializerType) {
		ArgumentNullException.ThrowIfNull(type);
		return Serializers.TryGetValue(type, out serializerType);
	}
}
