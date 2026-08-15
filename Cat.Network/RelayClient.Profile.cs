namespace Cat.Network;

public partial class RelayClient {
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

	protected override void OnAssignProfileMessage(IRelayTransport sender, Guid profileId) {
		ProfileId = profileId;
		UpdateAssignedProfile();
	}

	protected override void OnDeleteProfileMessage(IRelayTransport sender, Guid profileId) {
		if (ProfileId == profileId) {
			ProfileId = null;
			Profile = null;
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
		if (TryWriteProfileUpdateRequestMessage(MessageWriter, Profile)) {
			transport.Send(MessageWriter.GetWrittenSpan());
			ClearDirtyState(Profile);
		}
	}

	private void UpdateAssignedProfile() {
		if (ProfileId is { } profileId && ProfilesById.TryGetValue(profileId, out NetworkProfile? profile)) {
			Profile = profile;
		}
	}
}
