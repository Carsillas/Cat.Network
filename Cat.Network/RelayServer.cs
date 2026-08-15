namespace Cat.Network;

public partial class RelayServer(IDaemon daemon, TypeCatalogue typeCatalogue, IEntityStorage entityStorage) : RelayPeer(typeCatalogue) {
	private IDaemon Daemon { get; } = daemon ?? throw new ArgumentNullException(nameof(daemon));
	private IEntityStorage EntityStorage { get; } = entityStorage ?? throw new ArgumentNullException(nameof(entityStorage));
	private List<RemoteClient> Clients { get; } = [];
	private Dictionary<IRelayTransport, RemoteClient> ClientsByTransport { get; } = [];
	private Dictionary<Guid, RemoteClient> ClientsByProfileId { get; } = [];
	private Dictionary<Guid, Guid> OwnerProfileIdsByEntityId { get; } = [];
	private List<RemoteClient> ClientWorkingBuffer { get; } = [];
	private List<Guid> EntityWorkingBuffer { get; } = [];
	private List<Guid> ProfileWorkingBuffer { get; } = [];
	private List<NetworkEntity> RelevantEntityWorkingBuffer { get; } = [];
	private HashSet<NetworkProfile> SentProfileWorkingSet { get; } = [];
	private HashSet<NetworkEntity> DirtyEntityWorkingSet { get; } = [];
	private HashSet<Guid> RelevantEntityIdWorkingSet { get; } = [];
	private HashSet<IRelayTransport> FailedTransports { get; } = [];

	internal override bool Owns(NetworkEntity entity) {
		return true;
	}

	public void RemoveTransport(IRelayTransport transport) {
		ArgumentNullException.ThrowIfNull(transport);

		if (!ClientsByTransport.Remove(transport, out RemoteClient? client)) {
			return;
		}

		transport.MessageReceived -= ProcessMessage;
		transport.Disconnected -= OnTransportDisconnected;
		Clients.Remove(client);
		ClientsByProfileId.Remove(client.Profile.Id);

		EntityWorkingBuffer.Clear();
		EntityWorkingBuffer.AddRange(client.KnownEntityIds);
		foreach (Guid knownEntityId in EntityWorkingBuffer) {
			if (!IsOwner(client, knownEntityId)) {
				continue;
			}

			SetOwner(knownEntityId, null, ownerNotified: false);
			if (EntityStorage.TryGetEntity(knownEntityId, out NetworkEntity? entity) &&
			    entity.DestroyWithOwner) {
				EntityStorage.UnregisterEntity(knownEntityId);
				entity.Peer = null;
			}
		}

		EntityWorkingBuffer.Clear();
		client.ClearOwnership();
	}

	public void Tick() {
		Daemon.Tick();
		RemoveFailedTransports();

		while (Daemon.TryAcceptConnection(out IRelayTransport? transport, out NetworkProfile? profile)) {
			AddTransport(transport, profile);
		}

		ClientWorkingBuffer.Clear();
		ClientWorkingBuffer.AddRange(Clients);
		foreach (RemoteClient client in ClientWorkingBuffer) {
			try {
				client.Transport.PumpMessages();
			} catch (Exception) {
				MarkTransportFailed(client.Transport);
			}
		}

		ClientWorkingBuffer.Clear();
		RemoveFailedTransports();

		ProcessProfileRelevancy();
		RemoveFailedTransports();
		ProcessEntityRelevancy();
		RemoveFailedTransports();
	}

	private void AddTransport(IRelayTransport transport, NetworkProfile profile) {
		ArgumentNullException.ThrowIfNull(transport);
		ArgumentNullException.ThrowIfNull(profile);

		if (ClientsByTransport.ContainsKey(transport)) {
			return;
		}

		if (profile.Id == Guid.Empty) {
			profile.Id = Guid.NewGuid();
		}

		RemoteClient client = new(transport, profile);
		Clients.Add(client);
		ClientsByTransport.Add(transport, client);
		ClientsByProfileId.Add(profile.Id, client);
		transport.MessageReceived += ProcessMessage;
		transport.Disconnected += OnTransportDisconnected;
		FailedTransports.Remove(transport);
	}

	private bool TrySend(RemoteClient client, ReadOnlySpan<byte> message) {
		try {
			client.Transport.Send(message);
			return true;
		} catch (Exception) {
			MarkTransportFailed(client.Transport);
			return false;
		}
	}

	private void OnTransportDisconnected(IRelayTransport transport) {
		MarkTransportFailed(transport);
	}

	private void MarkTransportFailed(IRelayTransport transport) {
		FailedTransports.Add(transport);
	}

	private void RemoveFailedTransports() {
		if (FailedTransports.Count == 0) {
			return;
		}

		foreach (IRelayTransport transport in FailedTransports) {
			RemoveTransport(transport);
		}

		FailedTransports.Clear();
	}

	private sealed class RemoteClient(IRelayTransport transport, NetworkProfile profile) {
		public IRelayTransport Transport { get; } = transport;
		public NetworkProfile Profile { get; } = profile;
		public HashSet<Guid> KnownEntityIds { get; } = [];
		public HashSet<Guid> OwnedEntityIds { get; } = [];
		public HashSet<Guid> KnownProfileIds { get; } = [];

		public void RemoveOwnership(Guid entityId) {
			OwnedEntityIds.Remove(entityId);
		}

		public void ClearOwnership() {
			OwnedEntityIds.Clear();
		}
	}

}
