using System.Diagnostics.CodeAnalysis;

namespace Cat.Network;

public class TypeCatalogue {
	
	private Dictionary<Guid, Type> Types { get; } = [];
	private Dictionary<Type, INetworkObjectSerializer> Serializers { get; } = [];

	public TypeCatalogue Clone() {
		TypeCatalogue clone = new TypeCatalogue();
		foreach (KeyValuePair<Guid, Type> entry in Types) {
			clone.Types.Add(entry.Key, entry.Value);
		}
		foreach (KeyValuePair<Type, INetworkObjectSerializer> entry in Serializers) {
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
		if (!typeof(INetworkObjectSerializer).IsAssignableFrom(serializerType)) {
			throw new InvalidOperationException($"Serializer type '{serializerType.FullName}' does not implement '{typeof(INetworkObjectSerializer).FullName}'.");
		}

		if (Activator.CreateInstance(serializerType) is not INetworkObjectSerializer serializer) {
			throw new InvalidOperationException($"Serializer type '{serializerType.FullName}' could not be constructed.");
		}

		if (Types.TryGetValue(typeId.Id, out Type? existingType) && existingType != type) {
			throw new InvalidOperationException($"Type id '{typeId.Id}' is already registered to '{existingType.FullName}'.");
		}
		if (Serializers.TryGetValue(type, out INetworkObjectSerializer? existingSerializer) && existingSerializer.GetType() != serializerType) {
			throw new InvalidOperationException($"Type '{type.FullName}' is already registered to serializer '{existingSerializer.GetType().FullName}'.");
		}

		Types[typeId.Id] = type;
		Serializers[type] = serializer;
	}
	
	public bool TryFindType(Guid id, [NotNullWhen(true)] out Type? type) {
		return Types.TryGetValue(id, out type);
	}

	public bool TryFindSerializer(Type type, [NotNullWhen(true)] out INetworkObjectSerializer? serializer) {
		ArgumentNullException.ThrowIfNull(type);
		return Serializers.TryGetValue(type, out serializer);
	}
}
