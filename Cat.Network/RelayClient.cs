using System.Diagnostics.CodeAnalysis;

namespace Cat.Network;

public partial class RelayClient(TypeCatalogue typeCatalogue) : RelayPeer(typeCatalogue) {
	private const int EntityMessagePayloadLengthOffset = sizeof(byte) + sizeof(byte) + 16;
	private const int EntityMessagePayloadOffset = EntityMessagePayloadLengthOffset + sizeof(int);

	private IRelayTransport? Transport { get; set; }
	private HashSet<NetworkEntity> Entities { get; } = [];
	private Dictionary<Guid, NetworkEntity> EntitiesById { get; } = [];
	private HashSet<Guid> OwnedEntityIds { get; } = [];
	private HashSet<NetworkEntity> EntitiesToSpawn { get; } = [];
	private HashSet<NetworkEntity> EntitiesToDelete { get; } = [];
	private List<OwnershipTransferRequest> OwnershipTransferRequests { get; } = [];
	private Dictionary<Guid, NetworkProfile> ProfilesById { get; } = [];
	private Queue<BufferWriter> OutgoingMessageWriters { get; } = [];
	private Stack<BufferWriter> MessageWriterPool { get; } = [];
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

	[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
	public BufferWriter RentRpcMessageWriter(NetworkEntity entity, ulong rpcId, out SerializationContext context) {
		ArgumentNullException.ThrowIfNull(entity);
		if (Transport is null) {
			throw new InvalidOperationException("Cannot queue an RPC without a connected relay transport.");
		}
		if (entity.Peer is not RelayClient client || !ReferenceEquals(client, this)) {
			throw new InvalidOperationException("Cannot queue an RPC for a network entity that is not attached to this relay client.");
		}

		BufferWriter writer = RentMessageWriter();
		BeginRpcMessage(writer, entity.Id, rpcId);
		context = new SerializationContext(TypeCatalogue);
		return writer;
	}

	[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
	public BufferWriter RentBroadcastMessageWriter(NetworkEntity entity, ulong broadcastId, out SerializationContext context) {
		ArgumentNullException.ThrowIfNull(entity);
		if (Transport is null) {
			throw new InvalidOperationException("Cannot queue a broadcast without a connected relay transport.");
		}
		if (entity.Peer is not RelayClient client || !ReferenceEquals(client, this)) {
			throw new InvalidOperationException("Cannot queue a broadcast for a network entity that is not attached to this relay client.");
		}

		BufferWriter writer = RentMessageWriter();
		BeginBroadcastMessage(writer, entity.Id, broadcastId);
		context = new SerializationContext(TypeCatalogue);
		return writer;
	}

	[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
	public void QueueRentedMessageWriter(BufferWriter writer) {
		ArgumentNullException.ThrowIfNull(writer);
		if (writer.WrittenCount < EntityMessagePayloadOffset) {
			throw new InvalidOperationException("Cannot queue an incomplete relay message writer.");
		}

		writer.WriteInt32(EntityMessagePayloadLengthOffset..EntityMessagePayloadOffset, writer.WrittenCount - EntityMessagePayloadOffset);
		OutgoingMessageWriters.Enqueue(writer);
	}

	public void Tick() {
		if (Transport is null) {
			return;
		}

		Transport.PumpMessages();
		ProcessOutgoingMessages(Transport);
	}

	private BufferWriter RentMessageWriter() {
		if (MessageWriterPool.TryPop(out BufferWriter? writer)) {
			writer.Clear();
			return writer;
		}

		return new BufferWriter();
	}

	private static void BeginRpcMessage(BufferWriter writer, Guid entityId, ulong rpcId) {
		WriteEntityMessageHeader(writer, EntityMessageKind.Rpc, entityId);
		writer.Reserve(sizeof(int));
		writer.WriteUInt64(rpcId);
	}

	private static void BeginBroadcastMessage(BufferWriter writer, Guid entityId, ulong broadcastId) {
		WriteEntityMessageHeader(writer, EntityMessageKind.Broadcast, entityId);
		writer.Reserve(sizeof(int));
		writer.WriteUInt64(broadcastId);
	}

	private void ReturnMessageWriter(BufferWriter writer) {
		writer.Clear();
		MessageWriterPool.Push(writer);
	}
}
