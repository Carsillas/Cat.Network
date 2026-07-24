namespace Cat.Network;

public class TypeCatalogue {
	private readonly Dictionary<Guid, Type> typesById = [];

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

		if (typesById.TryGetValue(typeId.Id, out Type? existingType) && existingType != type) {
			throw new InvalidOperationException($"Type id '{typeId.Id}' is already registered to '{existingType.FullName}'.");
		}

		typesById[typeId.Id] = type;
	}
	
	public bool TryFindType(Guid id, out Type? type) {
		return typesById.TryGetValue(id, out type);
	}
}
