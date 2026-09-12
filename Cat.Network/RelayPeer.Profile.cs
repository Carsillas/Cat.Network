namespace Cat.Network;

public abstract partial class RelayPeer {
	private void ProcessProfileMessage(IRelayTransport sender, ReadOnlySpan<byte> message) {
		if (!message.TryConsumeProfileMessageKind(out ProfileMessageKind kind)) {
			return;
		}

		switch (kind) {
			case ProfileMessageKind.Assign: {
				if (!message.TryConsumeGuid(out Guid profileId)) {
					return;
				}

				OnAssignProfileMessage(sender, profileId);
				break;
			}
			case ProfileMessageKind.Create: {
				if (!message.TryConsumeGuid(out Guid profileId)) {
					return;
				}

				if (!message.TryConsumeGuid(out Guid profileTypeId)) {
					return;
				}

				if (message.TryConsumeLengthPrefixedData(out ReadOnlySpan<byte> profileData)) {
					OnCreateProfileMessage(sender, profileId, profileTypeId, profileData);
				}

				break;
			}
			case ProfileMessageKind.Update: {
				if (!message.TryConsumeGuid(out Guid profileId)) {
					return;
				}

				if (message.TryConsumeLengthPrefixedData(out ReadOnlySpan<byte> profileData)) {
					OnUpdateProfileMessage(sender, profileId, profileData);
				}

				break;
			}
			case ProfileMessageKind.Synchronize: {
				if (message.TryConsumeGuid(out Guid profileId) &&
				    message.TryConsumeUInt64(out ulong revision) &&
				    message.TryConsumeLengthPrefixedData(out ReadOnlySpan<byte> profileData)) {
					OnSynchronizeProfileMessage(sender, profileId, revision, profileData);
				}

				break;
			}
			case ProfileMessageKind.Delete: {
				if (!message.TryConsumeGuid(out Guid profileId)) {
					return;
				}

				OnDeleteProfileMessage(sender, profileId);
				break;
			}
			case ProfileMessageKind.UpdateRequest: {
				if (!message.TryConsumeGuid(out Guid profileTypeId)) {
					return;
				}

				if (message.TryConsumeLengthPrefixedData(out ReadOnlySpan<byte> profileData) &&
				    message.TryConsumeUInt64(out ulong revision)) {
					OnProfileUpdateRequest(sender, profileTypeId, profileData, revision);
				}

				break;
			}
			default:
				throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
		}
	}

	protected virtual void OnAssignProfileMessage(IRelayTransport sender, Guid profileId) {
	}

	protected virtual void OnCreateProfileMessage(IRelayTransport sender, Guid profileId, Guid typeId, ReadOnlySpan<byte> data) {
	}

	protected virtual void OnUpdateProfileMessage(IRelayTransport sender, Guid profileId, ReadOnlySpan<byte> data) {
	}

	protected virtual void OnSynchronizeProfileMessage(IRelayTransport sender, Guid profileId, ulong revision, ReadOnlySpan<byte> data) {
	}

	protected virtual void OnProfileUpdateRequest(IRelayTransport sender, Guid typeId, ReadOnlySpan<byte> data, ulong revision) {
		OnProfileUpdateRequest(sender, typeId, data);
	}

	protected virtual void OnProfileUpdateRequest(IRelayTransport sender, Guid typeId, ReadOnlySpan<byte> data) {
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
		ClearDirtyState(target);
		profile = target;
		return true;
	}

	protected bool TryDeserializeProfile(NetworkProfile profile, Guid typeId, ReadOnlySpan<byte> data) {
		if (!TypeCatalogue.TryFindType(typeId, out Type? type) ||
		    type != profile.GetType() ||
		    !TypeCatalogue.TryFindSerializer(type, out INetworkObjectSerializer? serializer)) {
			return false;
		}

		serializer.Deserialize(profile, data, new SerializationContext(TypeCatalogue));
		return true;
	}

	protected bool TryDeserializeProfileUpdate(NetworkProfile profile, ReadOnlySpan<byte> data) {
		if (!TypeCatalogue.TryFindSerializer(profile.GetType(), out INetworkObjectSerializer? serializer)) {
			return false;
		}

		serializer.Deserialize(profile, data, new SerializationContext(TypeCatalogue));
		return true;
	}

	protected bool TryWriteProfileMessage(BufferWriter writer, NetworkProfile profile) {
		return TryWriteCreateProfileMessage(writer, profile);
	}

	protected bool TryWriteCreateProfileMessage(BufferWriter writer, NetworkProfile profile) {
		if (!TypeCatalogue.TryFindTypeId(profile.GetType(), out Guid typeId) ||
		    !TypeCatalogue.TryFindSerializer(profile.GetType(), out INetworkObjectSerializer? serializer)) {
			return false;
		}

		WriteProfileMessageHeader(writer, ProfileMessageKind.Create, profile.Id);
		writer.WriteGuid(typeId);
		Range lengthRange = writer.Reserve(sizeof(int));
		int dataStart = writer.WrittenCount;
		serializer.Serialize(writer, profile, new SerializationContext(TypeCatalogue), new SerializationOptions(MemberSelectionMode.All, MemberIdentificationMode.Index));
		writer.WriteInt32(lengthRange, writer.WrittenCount - dataStart);
		return true;
	}

	protected bool TryWriteUpdateProfileMessage(BufferWriter writer, NetworkProfile profile) {
		if (!TypeCatalogue.TryFindSerializer(profile.GetType(), out INetworkObjectSerializer? serializer)) {
			return false;
		}

		WriteProfileMessageHeader(writer, ProfileMessageKind.Update, profile.Id);
		Range lengthRange = writer.Reserve(sizeof(int));
		int dataStart = writer.WrittenCount;
		serializer.Serialize(writer, profile, new SerializationContext(TypeCatalogue), new SerializationOptions(MemberSelectionMode.Dirty, MemberIdentificationMode.Index));
		writer.WriteInt32(lengthRange, writer.WrittenCount - dataStart);
		return true;
	}

	protected bool TryWriteSynchronizeProfileMessage(BufferWriter writer, NetworkProfile profile, ulong revision) {
		if (!TypeCatalogue.TryFindSerializer(profile.GetType(), out INetworkObjectSerializer? serializer)) {
			return false;
		}

		WriteProfileMessageHeader(writer, ProfileMessageKind.Synchronize, profile.Id);
		writer.WriteUInt64(revision);
		Range lengthRange = writer.Reserve(sizeof(int));
		int dataStart = writer.WrittenCount;
		serializer.Serialize(writer, profile, new SerializationContext(TypeCatalogue), new SerializationOptions(MemberSelectionMode.All, MemberIdentificationMode.Index));
		writer.WriteInt32(lengthRange, writer.WrittenCount - dataStart);
		return true;
	}

	protected bool TryWriteProfileUpdateRequestMessage(BufferWriter writer, NetworkProfile profile, ulong revision) {
		if (!TypeCatalogue.TryFindTypeId(profile.GetType(), out Guid typeId) ||
		    !TypeCatalogue.TryFindSerializer(profile.GetType(), out INetworkObjectSerializer? serializer)) {
			return false;
		}

		WriteProfileMessageHeader(writer, ProfileMessageKind.UpdateRequest);
		writer.WriteGuid(typeId);
		Range lengthRange = writer.Reserve(sizeof(int));
		int dataStart = writer.WrittenCount;
		serializer.Serialize(writer, profile, new SerializationContext(TypeCatalogue), new SerializationOptions(MemberSelectionMode.Dirty, MemberIdentificationMode.Index));
		writer.WriteInt32(lengthRange, writer.WrittenCount - dataStart);
		writer.WriteUInt64(revision);
		return true;
	}

	protected static void WriteAssignProfileMessage(BufferWriter writer, Guid profileId) {
		WriteProfileMessageHeader(writer, ProfileMessageKind.Assign, profileId);
	}

	protected static void WriteDeleteProfileMessage(BufferWriter writer, Guid profileId) {
		WriteProfileMessageHeader(writer, ProfileMessageKind.Delete, profileId);
	}

	private static void WriteProfileMessageHeader(BufferWriter writer, ProfileMessageKind kind, Guid profileId) {
		WriteProfileMessageHeader(writer, kind);
		writer.WriteGuid(profileId);
	}

	private static void WriteProfileMessageHeader(BufferWriter writer, ProfileMessageKind kind) {
		writer.WriteByte((byte)NetworkMessageChannel.ProfileMessage);
		writer.WriteByte((byte)kind);
	}
}
