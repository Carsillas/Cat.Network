namespace Cat.Network;

public partial class RelayServer {
	private void ProcessProfileRelevancy() {
		foreach (RemoteClient client in Clients) {
			DeleteUnknownProfiles(client);

			if (SendProfile(client, client.Profile)) {
				MessageWriter.Clear();
				WriteAssignProfileMessage(MessageWriter, client.Profile.Id);
				TrySend(client, MessageWriter.GetWrittenSpan());
			}

			foreach (RemoteClient otherClient in Clients) {
				SendProfile(client, otherClient.Profile);
			}
		}
	}

	private void DeleteUnknownProfiles(RemoteClient client) {
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

	private bool SendProfile(RemoteClient client, NetworkProfile profile) {
		if (!client.KnownProfileIds.Add(profile.Id)) {
			return false;
		}

		MessageWriter.Clear();
		if (TryWriteProfileMessage(MessageWriter, profile)) {
			if (!TrySend(client, MessageWriter.GetWrittenSpan())) {
				client.KnownProfileIds.Remove(profile.Id);
				return false;
			}

			ClearDirtyState(profile);
			return true;
		} else {
			client.KnownProfileIds.Remove(profile.Id);
			return false;
		}
	}
}
