using System.Diagnostics.CodeAnalysis;

namespace Cat.Network;

public partial class RelayClient(TypeCatalogue typeCatalogue) : RelayPeer(typeCatalogue) {
	private IRelayTransport? Transport { get; set; }
	private HashSet<NetworkEntity> Entities { get; } = [];
	private Dictionary<Guid, NetworkEntity> EntitiesById { get; } = [];
	private HashSet<Guid> OwnedEntityIds { get; } = [];
	private HashSet<NetworkEntity> EntitiesToSpawn { get; } = [];
	private HashSet<NetworkEntity> EntitiesToDelete { get; } = [];
	private List<OwnershipTransferRequest> OwnershipTransferRequests { get; } = [];
	private Dictionary<Guid, NetworkProfile> ProfilesById { get; } = [];
	private Guid? ProfileId { get; set; }

	public NetworkProfile? Profile { get; private set; }

	internal override bool Owns(NetworkEntity entity) {
		return OwnedEntityIds.Contains(entity.Id);
	}

	public bool TryGetEntity(Guid id, [NotNullWhen(true)] out NetworkEntity? entity) {
		return EntitiesById.TryGetValue(id, out entity);
	}

	public bool TryGetProfile(Guid id, [NotNullWhen(true)] out NetworkProfile? profile) {
		return ProfilesById.TryGetValue(id, out profile);
	}

	public void Connect(IRelayTransport transport) {
		if (Transport is not null) {
			Transport.MessageReceived -= ProcessMessage;
		}

		Transport = transport ?? throw new ArgumentNullException(nameof(transport));
		Transport.MessageReceived += ProcessMessage;
	}

	public void Disconnect() {
		if (Transport is not null) {
			Transport.MessageReceived -= ProcessMessage;
		}

		Transport = null;
	}

	public void Tick() {
		if (Transport is null) {
			return;
		}

		Transport.PumpMessages();
		ProcessOutgoingMessages(Transport);
	}
}
