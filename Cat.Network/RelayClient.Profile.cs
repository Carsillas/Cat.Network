namespace Cat.Network;

public partial class RelayClient {
	protected override void OnCreateProfileMessage(IRelayTransport sender, Guid profileId, Guid typeId, ReadOnlySpan<byte> data) {
		long sessionVersion = SessionVersion;
		if (ProfilesById.ContainsKey(profileId)) {
			return;
		}

		if (!TryCreateProfile(profileId, typeId, data, out NetworkProfile profile) || SessionVersion != sessionVersion) {
			return;
		}

		RegisterProfile(profile);
		if (SessionVersion != sessionVersion) {
			return;
		}
		UpdateAssignedProfile();
		ClearDirtyState(profile);
	}

	protected override void OnUpdateProfileMessage(IRelayTransport sender, Guid profileId, ReadOnlySpan<byte> data) {
		long sessionVersion = SessionVersion;
		if (!ProfilesById.TryGetValue(profileId, out NetworkProfile? profile) ||
		    !TryDeserializeProfileUpdate(profile, data) || SessionVersion != sessionVersion) {
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
		long sessionVersion = SessionVersion;
		if (ProfilesById.TryGetValue(profile.Id, out NetworkProfile? existingProfile)) {
			if (ReferenceEquals(existingProfile, profile)) {
				return false;
			}

			UnregisterProfile(profile.Id);
			if (SessionVersion != sessionVersion) {
				return false;
			}
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

	private void ProcessOutgoingProfileMessage(IRelayTransport transport, long sessionVersion) {
		if (Profile is not { } profile || !HasDirtyState(profile)) {
			return;
		}

		MessageWriter.Clear();
		if (TryWriteProfileUpdateRequestMessage(MessageWriter, profile) &&
		    TrySendSessionMessage(transport, sessionVersion, MessageWriter.GetWrittenSpan())) {
			ClearDirtyState(profile);
		}
	}

	private void UpdateAssignedProfile() {
		if (ProfileId is { } profileId && ProfilesById.TryGetValue(profileId, out NetworkProfile? profile)) {
			Profile = profile;
		}
	}
}
