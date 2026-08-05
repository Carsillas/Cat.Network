namespace Cat.Network;

public abstract partial class RelayPeer {
	private void ProcessProfileMessage(IRelayTransport sender, ReadOnlySpan<byte> message) {
		if (!message.TryConsumeProfileMessageKind(out ProfileMessageKind kind)) {
			return;
		}

		if (!message.TryConsumeGuid(out Guid profileId)) {
			return;
		}

		switch (kind) {
			case ProfileMessageKind.Assign:
				OnAssignProfileMessage(sender, profileId);
				break;
			case ProfileMessageKind.CreateOrUpdate:
				if (!message.TryConsumeGuid(out Guid profileTypeId)) {
					return;
				}

				if (message.TryConsumeLengthPrefixedData(out ReadOnlySpan<byte> profileData)) {
					OnProfileMessage(sender, profileId, profileTypeId, profileData);
				}

				break;
			case ProfileMessageKind.Delete:
				OnDeleteProfileMessage(sender, profileId);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
		}
	}

	protected virtual void OnAssignProfileMessage(IRelayTransport sender, Guid profileId) {
	}

	protected virtual void OnProfileMessage(IRelayTransport sender, Guid profileId, Guid typeId, ReadOnlySpan<byte> data) {
	}

	protected virtual void OnDeleteProfileMessage(IRelayTransport sender, Guid profileId) {
	}

	protected bool TryCreateProfile(Guid profileId, Guid typeId, ReadOnlySpan<byte> data, out NetworkProfile profile) {
		profile = null!;

		if (!TypeCatalogue.TryFindType(typeId, out Type? type) ||
		    !typeof(NetworkProfile).IsAssignableFrom(type) ||
		    !TypeCatalogue.TryFindSerializer(type, out INetworkObjectSerializer? serializer) ||
		    Activator.CreateInstance(type) is not NetworkProfile target) {
			return false;
		}

		target.Id = profileId;
		serializer.Deserialize(target, data, new SerializationContext(TypeCatalogue));
		profile = target;
		return true;
	}

	protected bool TryWriteProfileMessage(BufferWriter writer, NetworkProfile profile) {
		if (!TypeCatalogue.TryFindTypeId(profile.GetType(), out Guid typeId) ||
		    !TypeCatalogue.TryFindSerializer(profile.GetType(), out INetworkObjectSerializer? serializer)) {
			return false;
		}

		WriteProfileMessageHeader(writer, ProfileMessageKind.CreateOrUpdate, profile.Id);
		writer.WriteGuid(typeId);
		Range lengthRange = writer.Reserve(sizeof(int));
		int dataStart = writer.WrittenCount;
		serializer.Serialize(writer, profile, new SerializationContext(TypeCatalogue), new SerializationOptions(MemberSelectionMode.All, MemberIdentificationMode.Index));
		writer.WriteInt32(lengthRange, writer.WrittenCount - dataStart);
		return true;
	}

	protected static void WriteAssignProfileMessage(BufferWriter writer, Guid profileId) {
		WriteProfileMessageHeader(writer, ProfileMessageKind.Assign, profileId);
	}

	protected static void WriteDeleteProfileMessage(BufferWriter writer, Guid profileId) {
		WriteProfileMessageHeader(writer, ProfileMessageKind.Delete, profileId);
	}

	private static void WriteProfileMessageHeader(BufferWriter writer, ProfileMessageKind kind, Guid profileId) {
		writer.WriteByte((byte)NetworkMessageChannel.ProfileMessage);
		writer.WriteByte((byte)kind);
		writer.WriteGuid(profileId);
	}
}
