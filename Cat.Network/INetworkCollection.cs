namespace Cat.Network;

public interface INetworkCollection {
	void Initialize(NetworkObject owner, int propertyIndex);

	void Serialize(BufferWriter writer, SerializationContext context, SerializationOptions options);

	/// <summary>Applies serialized operations in order, retaining them for forwarding.</summary>
	/// <remarks>
	/// Deserialization is not transactional. Invalid collection framing or operations throw
	/// <see cref="InvalidOperationException"/>; earlier applied changes and pending operations
	/// remain tracked on the owner. New values are decoded before attachment. Updates to
	/// existing objects retain any changes made before their deserializer throws, and forward
	/// the object's current dirty state. Nested serializers retain their own validation rules.
	/// Collection events run synchronously after the mutation has been recorded. If a handler
	/// throws, its exception propagates, the applied mutation remains tracked, and subsequent
	/// operations and event notifications are not processed.
	/// </remarks>
	void Deserialize(ReadOnlySpan<byte> data, SerializationContext context);

	void ClearDirtyState(SerializationContext context);
}
