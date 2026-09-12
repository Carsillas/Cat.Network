namespace Cat.Network;

public interface INetworkCollection {
	void Initialize(NetworkObject owner, int propertyIndex);

	void Serialize(BufferWriter writer, SerializationContext context, SerializationOptions options);

	/// <summary>Applies serialized operations in order, retaining them for forwarding.</summary>
	/// <remarks>
	/// Collection events run synchronously after the mutation and its owner dirty state
	/// have been recorded. Event handlers may mutate the collection; those operations are
	/// forwarded after the incoming operation. If a handler throws, its exception propagates,
	/// applied mutations remain tracked, and later operations and notifications are not processed.
	/// Updates to existing objects retain the original target before invoking its deserializer,
	/// so nested callbacks can remove or replace that target without reordering the update.
	/// Pending updates serialize the target's current dirty state, including changes applied
	/// before an exception; deserialization is not transactional.
	/// </remarks>
	void Deserialize(ReadOnlySpan<byte> data, SerializationContext context);

	void ClearDirtyState(SerializationContext context);
}
