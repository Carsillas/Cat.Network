namespace Cat.Network;

public partial class RelayClient {
	protected override void OnProfileMessage(IRelayTransport sender, Guid profileId, Guid typeId, ReadOnlySpan<byte> data) {
		if (!TryCreateProfile(profileId, typeId, data, out NetworkProfile profile)) {
			return;
		}

		ProfilesById[profile.Id] = profile;
		UpdateProfile();
		ClearDirtyState(profile);
	}

	protected override void OnAssignProfileMessage(IRelayTransport sender, Guid profileId) {
		ProfileId = profileId;
		UpdateProfile();
	}

	protected override void OnDeleteProfileMessage(IRelayTransport sender, Guid profileId) {
		ProfilesById.Remove(profileId);
		if (ProfileId == profileId) {
			ProfileId = null;
			Profile = null;
		}
	}

	private void UpdateProfile() {
		if (ProfileId is Guid profileId && ProfilesById.TryGetValue(profileId, out NetworkProfile? profile)) {
			Profile = profile;
		}
	}
}
