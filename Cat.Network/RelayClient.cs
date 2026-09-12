using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Cat.Network;

public partial class RelayClient(TypeCatalogue typeCatalogue) : RelayPeer(typeCatalogue) {
	private const int EntityMessagePayloadLengthOffset = sizeof(byte) + sizeof(byte) + 16;
	private const int EntityMessagePayloadOffset = EntityMessagePayloadLengthOffset + sizeof(int);
	private static readonly object MessageWriterLease = new();

	private IRelayTransport? Transport { get; set; }
	private MessageHandler? TransportMessageHandler { get; set; }
	private Action<IRelayTransport>? TransportDisconnectedHandler { get; set; }
	private long SessionVersion { get; set; }
	private bool IsTicking { get; set; }
	private HashSet<NetworkEntity> Entities { get; } = [];
	private Dictionary<Guid, NetworkEntity> EntitiesById { get; } = [];
	private HashSet<Guid> OwnedEntityIds { get; } = [];
	private HashSet<NetworkEntity> EntitiesToSpawn { get; } = [];
	private HashSet<NetworkEntity> EntitiesToDelete { get; } = [];
	private List<OwnershipTransferRequest> OwnershipTransferRequests { get; } = [];
	private Dictionary<Guid, NetworkProfile> ProfilesById { get; } = [];
	private Queue<BufferWriter> OutgoingMessageWriters { get; } = [];
	// Serialization can throw before queueing, so an abandoned rental must remain collectible.
	private ConditionalWeakTable<BufferWriter, object> RentedMessageWriters { get; } = new();
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

	/// <summary>
	/// Connects a caller-owned transport. Replacing an active transport ends its session;
	/// connecting the same active transport is a no-op. Entities prepared while disconnected are retained.
	/// </summary>
	public void Connect(IRelayTransport transport) {
		ArgumentNullException.ThrowIfNull(transport);
		if (ReferenceEquals(Transport, transport)) {
			return;
		}

		if (Transport is not null) {
			long endedSessionVersion = EndSession();
			if (SessionVersion != endedSessionVersion || Transport is not null) {
				// A lifecycle callback made a newer connection decision.
				return;
			}
		}

		Transport = transport;
		long sessionVersion = SessionVersion;
		TransportMessageHandler = (sender, message) => {
			if (IsCurrentSession(transport, sessionVersion) && ReferenceEquals(sender, transport)) {
				ProcessMessage(sender, message);
			}
		};
		TransportDisconnectedHandler = sender => {
			if (IsCurrentSession(transport, sessionVersion) && ReferenceEquals(sender, transport)) {
				Disconnect();
			}
		};
		transport.MessageReceived += TransportMessageHandler;
		transport.Disconnected += TransportDisconnectedHandler;
	}

	/// <summary>
	/// Detaches all entities, clears profiles and pending output, and raises lifecycle notifications.
	/// The caller remains responsible for closing or disposing the transport.
	/// </summary>
	public void Disconnect() {
		EndSession();
	}

	private long EndSession() {
		long sessionVersion = ++SessionVersion;
		IRelayTransport? transport = Transport;
		Transport = null;
		if (transport is not null) {
			transport.MessageReceived -= TransportMessageHandler;
			transport.Disconnected -= TransportDisconnectedHandler;
		}
		TransportMessageHandler = null;
		TransportDisconnectedHandler = null;

		var entities = EntitiesById.Values.Select(entity => (Entity: entity, WasOwner: Owns(entity))).ToArray();
		NetworkProfile[] profiles = ProfilesById.Values.ToArray();
		Entities.Clear();
		EntitiesById.Clear();
		OwnedEntityIds.Clear();
		EntitiesToSpawn.Clear();
		EntitiesToDelete.Clear();
		OwnershipTransferRequests.Clear();
		ProfilesById.Clear();
		ProfileId = null;
		Profile = null;
		RentedMessageWriters.Clear();
		while (OutgoingMessageWriters.TryDequeue(out BufferWriter? writer)) {
			ReturnMessageWriter(writer);
		}
		MessageWriter.Clear();
		foreach (var (entity, _) in entities) {
			entity.Peer = null;
		}

		// Clear the entire session before callbacks can prepare or connect a new one.
		foreach (var (entity, wasOwner) in entities) {
			if (wasOwner) {
				RaiseEntityEvent(EntityOwnershipLostHandlers, entity);
			}
			RaiseEntityEvent(EntityUnobservedHandlers, entity);
		}
		foreach (NetworkProfile profile in profiles) {
			RaiseProfileEvent(ProfileLeftHandlers, profile);
		}
		return sessionVersion;
	}

	private bool IsCurrentSession(IRelayTransport transport, long sessionVersion) {
		return SessionVersion == sessionVersion && ReferenceEquals(Transport, transport);
	}

	private bool TrySendSessionMessage(IRelayTransport transport, long sessionVersion, ReadOnlySpan<byte> message) {
		if (!IsCurrentSession(transport, sessionVersion)) {
			return false;
		}

		transport.Send(message);
		return IsCurrentSession(transport, sessionVersion);
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
		if (Transport is null || !RentedMessageWriters.Remove(writer)) {
			throw new InvalidOperationException("Cannot queue a message writer that was not rented in the current relay session.");
		}

		writer.WriteInt32(EntityMessagePayloadLengthOffset..EntityMessagePayloadOffset, writer.WrittenCount - EntityMessagePayloadOffset);
		OutgoingMessageWriters.Enqueue(writer);
	}

	public void Tick() {
		if (Transport is not { } transport || IsTicking) {
			return;
		}

		long sessionVersion = SessionVersion;
		IsTicking = true;
		try {
			transport.PumpMessages();
			if (!IsCurrentSession(transport, sessionVersion)) {
				return;
			}
			ProcessOutgoingProfileMessage(transport, sessionVersion);
			if (IsCurrentSession(transport, sessionVersion)) {
				ProcessOutgoingMessages(transport, sessionVersion);
			}
		} finally {
			IsTicking = false;
		}
	}

	private BufferWriter RentMessageWriter() {
		if (!MessageWriterPool.TryPop(out BufferWriter? writer)) {
			writer = new BufferWriter();
		} else {
			writer.Clear();
		}

		RentedMessageWriters.Add(writer, MessageWriterLease);
		return writer;
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
