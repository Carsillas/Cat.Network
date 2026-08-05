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
	private HashSet<NetworkEntity> DirtyEntityWorkingSet { get; } = [];
	private HashSet<Guid> RelevantEntityIdWorkingSet { get; } = [];

	internal override bool Owns(NetworkEntity entity) {
		return true;
	}

	public void RemoveTransport(IRelayTransport transport) {
		ArgumentNullException.ThrowIfNull(transport);

		if (!ClientsByTransport.Remove(transport, out RemoteClient? client)) {
			return;
		}

		transport.MessageReceived -= ProcessMessage;
		Clients.Remove(client);
		ClientsByProfileId.Remove(client.Profile.Id);

		EntityWorkingBuffer.Clear();
		EntityWorkingBuffer.AddRange(client.KnownEntityIds);
		foreach (Guid knownEntityId in EntityWorkingBuffer) {
			if (IsOwner(client, knownEntityId)) {
				SetOwner(knownEntityId, null, ownerNotified: false);
			}
		}

		EntityWorkingBuffer.Clear();
		client.ClearOwnership();
	}

	public void Tick() {
		Daemon.Tick();
		while (Daemon.TryAcceptConnection(out IRelayTransport? transport, out NetworkProfile? profile)) {
			AddTransport(transport, profile);
		}

		ClientWorkingBuffer.Clear();
		ClientWorkingBuffer.AddRange(Clients);
		foreach (RemoteClient client in ClientWorkingBuffer) {
			client.Transport.PumpMessages();
		}

		ClientWorkingBuffer.Clear();

		ProcessProfileRelevancy();
		ProcessEntityRelevancy();
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
