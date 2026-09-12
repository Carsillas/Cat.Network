namespace Cat.Network;

public partial class RelayClient {
	private ulong SentProfileRevision { get; set; }
	private BufferWriter ProfileSnapshotWriter { get; } = new();

	protected override void OnCreateProfileMessage(IRelayTransport sender, Guid profileId, Guid typeId, ReadOnlySpan<byte> data) {
		if (ProfilesById.ContainsKey(profileId)) {
			return;
		}

		if (!TryCreateProfile(profileId, typeId, data, out NetworkProfile profile)) {
			return;
		}

		RegisterProfile(profile);
		UpdateAssignedProfile();
		ClearDirtyState(profile);
	}

	protected override void OnUpdateProfileMessage(IRelayTransport sender, Guid profileId, ReadOnlySpan<byte> data) {
		if (!ProfilesById.TryGetValue(profileId, out NetworkProfile? profile) ||
		    !TryDeserializeProfileUpdate(profile, data)) {
			return;
		}

		UpdateAssignedProfile();
		ClearDirtyState(profile);
	}

	protected override void OnSynchronizeProfileMessage(IRelayTransport sender, Guid profileId, ulong revision, ReadOnlySpan<byte> data) {
		if (ProfileId != profileId ||
		    !ProfilesById.TryGetValue(profileId, out NetworkProfile? profile) ||
		    revision != SentProfileRevision || HasDirtyState(profile)) {
			// The next request will receive a fresh snapshot including the deferred server changes.
			return;
		}

		if (!TypeCatalogue.TryFindSerializer(profile.GetType(), out INetworkObjectSerializer? serializer)) {
			return;
		}

		// An unchanged acknowledgement must not replay collection events or replace child objects.
		ProfileSnapshotWriter.Clear();
		SerializationContext context = new(TypeCatalogue);
		serializer.Serialize(ProfileSnapshotWriter, profile, context, new SerializationOptions(MemberSelectionMode.All, MemberIdentificationMode.Index));
		if (data.SequenceEqual(ProfileSnapshotWriter.GetWrittenSpan())) {
			return;
		}

		serializer.Deserialize(profile, data, context);
		ClearDirtyState(profile);
	}

	protected override void OnAssignProfileMessage(IRelayTransport sender, Guid profileId) {
		if (ProfileId != profileId) {
			SentProfileRevision = 0;
		}

		ProfileId = profileId;
		UpdateAssignedProfile();
	}

	protected override void OnDeleteProfileMessage(IRelayTransport sender, Guid profileId) {
		if (ProfileId == profileId) {
			ProfileId = null;
			Profile = null;
			SentProfileRevision = 0;
		}

		UnregisterProfile(profileId);
	}

	private bool RegisterProfile(NetworkProfile profile) {
		if (ProfilesById.TryGetValue(profile.Id, out NetworkProfile? existingProfile)) {
			if (ReferenceEquals(existingProfile, profile)) {
				return false;
			}

			UnregisterProfile(profile.Id);
		}

		ProfilesById[profile.Id] = profile;
		RaiseProfileEvent(ProfileJoinedHandlers, profile);
		return true;
	}

	private bool UnregisterProfile(Guid profileId) {
		if (!ProfilesById.Remove(profileId, out NetworkProfile? profile)) {
			return false;
		}

		RaiseProfileEvent(ProfileLeftHandlers, profile);
		return true;
	}

	private void ProcessOutgoingProfileMessage(IRelayTransport transport) {
		if (Profile is null || !HasDirtyState(Profile)) {
			return;
		}

		MessageWriter.Clear();
		ulong revision = checked(SentProfileRevision + 1);
		if (TryWriteProfileUpdateRequestMessage(MessageWriter, Profile, revision)) {
			transport.Send(MessageWriter.GetWrittenSpan());
			SentProfileRevision = revision;
			ClearDirtyState(Profile);
		}
	}

	private void UpdateAssignedProfile() {
		if (ProfileId is { } profileId && ProfilesById.TryGetValue(profileId, out NetworkProfile? profile)) {
			Profile = profile;
		}
	}
}
