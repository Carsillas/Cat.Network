namespace Cat.Network;

public partial class RelayServer {
	protected override void OnCreateProfileMessage(IRelayTransport sender, Guid profileId, Guid typeId, ReadOnlySpan<byte> data) {
	}

	protected override void OnProfileUpdateRequest(IRelayTransport sender, Guid typeId, ReadOnlySpan<byte> data) {
		if (!ClientsByTransport.TryGetValue(sender, out RemoteClient? client) ||
		    !TryDeserializeProfile(client.Profile, typeId, data)) {
			return;
		}
	}

	private void ProcessProfileRelevancy() {
		SentProfileWorkingSet.Clear();

		foreach (RemoteClient client in Clients) {
			DeleteStaleProfiles(client);

			foreach (RemoteClient otherClient in Clients) {
				if (client.KnownProfileIds.Contains(otherClient.Profile.Id)) {
					if (!HasDirtyState(otherClient.Profile) ||
					    !SendProfileUpdate(client, otherClient.Profile)) {
						continue;
					}

					SentProfileWorkingSet.Add(otherClient.Profile);
					continue;
				}

				if (!SendProfileCreate(client, otherClient.Profile)) {
					continue;
				}

				bool becameKnown = client.KnownProfileIds.Add(otherClient.Profile.Id);
				SentProfileWorkingSet.Add(otherClient.Profile);

				if (ReferenceEquals(otherClient, client) && becameKnown) {
					MessageWriter.Clear();
					WriteAssignProfileMessage(MessageWriter, client.Profile.Id);
					TrySend(client, MessageWriter.GetWrittenSpan());
				}
			}
		}

		foreach (NetworkProfile profile in SentProfileWorkingSet) {
			ClearDirtyState(profile);
		}

		SentProfileWorkingSet.Clear();
	}

	private void DeleteStaleProfiles(RemoteClient client) {
		ProfileWorkingBuffer.Clear();
		foreach (Guid knownProfileId in client.KnownProfileIds) {
			if (!ClientsByProfileId.ContainsKey(knownProfileId)) {
				ProfileWorkingBuffer.Add(knownProfileId);
			}
		}

		foreach (Guid profileId in ProfileWorkingBuffer) {
			client.KnownProfileIds.Remove(profileId);
			MessageWriter.Clear();
			WriteDeleteProfileMessage(MessageWriter, profileId);
			TrySend(client, MessageWriter.GetWrittenSpan());
		}

		ProfileWorkingBuffer.Clear();
	}

	private bool SendProfileCreate(RemoteClient client, NetworkProfile profile) {
		MessageWriter.Clear();
		if (TryWriteCreateProfileMessage(MessageWriter, profile)) {
			return TrySend(client, MessageWriter.GetWrittenSpan());
		}

		return false;
	}

	private bool SendProfileUpdate(RemoteClient client, NetworkProfile profile) {
		MessageWriter.Clear();
		if (TryWriteUpdateProfileMessage(MessageWriter, profile)) {
			return TrySend(client, MessageWriter.GetWrittenSpan());
		}

		return false;
	}
}
