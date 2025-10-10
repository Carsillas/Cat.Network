using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using static Cat.Network.CatServer;
using static Cat.Network.SerializationUtils;

namespace Cat.Network;
internal class RemoteClient : IEntityProcessor {

	internal HashSet<NetworkEntity> OwnedEntities { get; } = new();
	private HashSet<NetworkEntity> InternalRelevantEntities { get; } = new();
	public IEntityProcessor.FastEnumerable RelevantEntities => new(InternalRelevantEntities.GetEnumerator());


	private byte[] OutgoingReliableDataBuffer = new byte[1_000_000];

	private ISerializationContext SerializationContext { get; }
	internal ITransport Transport { get; }
	internal NetworkEntity ProfileEntity { get; }
	internal PacketProcessor CachedPacketProcessor { get; set; }
	internal CatServer Server { get; set; }


	internal RemoteClient(ISerializationContext serializationContext, ITransport transport, NetworkEntity profileEntity) {
		SerializationContext = serializationContext;
		Transport = transport;
		ProfileEntity = profileEntity;
	}

	public void CreateOrUpdate(NetworkEntity entity) {
		if (InternalRelevantEntities.Add(entity)) {
			CreateEntity(entity);
		} else {
			UpdateEntity(entity);
		}
	}
	public void DeleteEntity(NetworkEntity entity) {

		Server.UnassignIfOwned(ProfileEntity, entity);

		OwnedEntities.Remove(entity);
		InternalRelevantEntities.Remove(entity);

		int headerLength = WritePacketHeader(OutgoingReliableDataBuffer, RequestType.DeleteEntity, entity, out Span<byte> contentBuffer);
		Transport.SendPacket(OutgoingReliableDataBuffer, headerLength);
	}
	
	public void UnassignEntity(NetworkEntity entity) {

		Server.UnassignIfOwned(ProfileEntity, entity);

		OwnedEntities.Remove(entity);
		
		// No packet send, the only way an entity can become unowned is via deletion or forfeiting ownership.
	}

	internal void RegisterSpawnedEntity(NetworkEntity entity) {
		InternalRelevantEntities.Add(entity);
		OwnedEntities.Add(entity);
	}
	
	private void CreateEntity(NetworkEntity entity) {
		bool isOwner = Server.AssignIfOwnerless(ProfileEntity, entity);

		INetworkEntity iEntity = entity;
		int headerLength = WritePacketHeader(OutgoingReliableDataBuffer, RequestType.CreateEntity, entity, out Span<byte> contentBuffer);
		int contentLength = iEntity.Serialize(CreateOptions, contentBuffer);

		Transport.SendPacket(OutgoingReliableDataBuffer, headerLength + contentLength);

		if (isOwner) {
			if (OwnedEntities.Add(entity)) {
				NotifyAssignedOwner(entity);
			}
			SendOutgoingRpcs(entity);
		} else {
			SendOutgoingBroadcasts(entity);
		}

	}

	private void UpdateEntity(NetworkEntity entity) {
		INetworkEntity iEntity = entity;

		bool isOwner = Server.AssignIfOwnerless(ProfileEntity, entity);

		bool isDirty = ((INetworkEntity)entity).SerializationContext.Time == iEntity.LastDirtyTick;

		if (isDirty) {
			int headerLength = WritePacketHeader(OutgoingReliableDataBuffer, RequestType.UpdateEntity, entity, out Span<byte> contentBuffer);
			int contentLength = iEntity.Serialize(UpdateOptions, contentBuffer);

			Transport.SendPacket(OutgoingReliableDataBuffer, headerLength + contentLength);
		} 

		if (isOwner) {
			if (OwnedEntities.Add(entity)) {
				NotifyAssignedOwner(entity);
			}
			SendOutgoingRpcs(entity);
		} else {
			SendOutgoingBroadcasts(entity);
		}

	}

	private void NotifyAssignedOwner(NetworkEntity entity) {
		int headerLength = WritePacketHeader(OutgoingReliableDataBuffer, RequestType.AssignOwner, entity, out Span<byte> contentBuffer);
		Transport.SendPacket(OutgoingReliableDataBuffer, headerLength);
	}

	private void SendOutgoingRpcs(NetworkEntity entity) {
		List<byte[]> outgoingRpcs = SerializationContext.GetOutgoingRpcs(entity);

		if (outgoingRpcs != null) {
			foreach (byte[] rpc in outgoingRpcs) {
				const int ServerRpcHeaderLength = 17;
				const int ServertRpcContentLengthSlot = 4;
				int length = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(rpc, ServerRpcHeaderLength, 4));
				Transport.SendPacket(rpc, length + ServerRpcHeaderLength + ServertRpcContentLengthSlot);
			}
		}
	}
	
	
	private void SendOutgoingBroadcasts(NetworkEntity entity) {
		List<byte[]> outgoingBroadcasts = SerializationContext.GetOutgoingBroadcasts(entity);

		if (outgoingBroadcasts != null) {
			foreach (byte[] rpc in outgoingBroadcasts) {
				const int ServerRpcHeaderLength = 17;
				const int ServertRpcContentLengthSlot = 4;
				int length = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(rpc, ServerRpcHeaderLength, 4));
				Transport.SendPacket(rpc, length + ServerRpcHeaderLength + ServertRpcContentLengthSlot);
			}
		}
		
		outgoingBroadcasts = SerializationContext.GetOutgoingBroadcasts(entity);
	}

}
