namespace Cat.Network;

public partial class RelayClient {
	protected override void OnCreateProfileMessage(IRelayTransport sender, Guid profileId, Guid typeId, ReadOnlySpan<byte> data) {
		if (!TryCreateProfile(profileId, typeId, data, out NetworkProfile profile)) {
			return;
		}

		ProfilesById[profile.Id] = profile;
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
		ProfilesById.Remove(profileId);
		if (ProfileId == profileId) {
			ProfileId = null;
			Profile = null;
		}
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
