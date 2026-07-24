namespace Cat.Network;

public readonly struct SerializationContext(TypeCatalogue typeCatalogue) {
	public TypeCatalogue TypeCatalogue { get; } = typeCatalogue ?? throw new ArgumentNullException(nameof(typeCatalogue));
}
