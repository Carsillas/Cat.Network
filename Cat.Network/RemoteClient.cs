using System;
using System.Collections.Generic;
using static Cat.Network.SerializationUtils;

namespace Cat.Network;
internal class RemoteClient : IEntityProcessor {

	public HashSet<NetworkEntity> OwnedEntities { get; } = new HashSet<NetworkEntity>();
	public HashSet<NetworkEntity> RelevantEntities { get; } = new HashSet<NetworkEntity>();
	IReadOnlySet<NetworkEntity> IEntityProcessor.RelevantEntities => RelevantEntities;


	private byte[] OutgoingReliableDataBuffer = new byte[1_000_000];


	public ITransport Transport { get; }
	public NetworkEntity ProfileEntity { get; }


	public RemoteClient(ITransport transport, NetworkEntity profileEntity) {
		Transport = transport;
		ProfileEntity = profileEntity;
	}



	public void NotifyAssignedOwner(NetworkEntity entity) {
		int headerLength = WritePacketHeader(OutgoingReliableDataBuffer, RequestType.AssignOwner, entity, out Span<byte> contentBuffer);
		Transport.SendPacket(OutgoingReliableDataBuffer, headerLength);
	}

	public void CreateEntity(NetworkEntity entity, bool isOwner) {
		RelevantEntities.Add(entity);
		INetworkEntity iEntity = entity;
		int headerLength = WritePacketHeader(OutgoingReliableDataBuffer, RequestType.CreateEntity, entity, out Span<byte> contentBuffer);
		int contentLength = iEntity.Serialize(CreateOptions, contentBuffer);

		Transport.SendPacket(OutgoingReliableDataBuffer, headerLength + contentLength);

		if (isOwner && OwnedEntities.Add(entity)) {
			NotifyAssignedOwner(entity);
		}
	}

	public void UpdateEntity(NetworkEntity entity, bool isOwner) {
		INetworkEntity iEntity = entity;

		bool isDirty = ((INetworkEntity)entity).SerializationContext.Time == iEntity.LastDirtyTick;

		if (isDirty) {
			int headerLength = WritePacketHeader(OutgoingReliableDataBuffer, RequestType.UpdateEntity, entity, out Span<byte> contentBuffer);
			int contentLength = iEntity.Serialize(UpdateOptions, contentBuffer);

			Transport.SendPacket(OutgoingReliableDataBuffer, headerLength + contentLength);
		} else if (iEntity.LastDirtyTick > -1) {
			iEntity.LastDirtyTick = -1;
			iEntity.Clean();
		}

		if (isOwner && OwnedEntities.Add(entity)) {
			NotifyAssignedOwner(entity);
		}

	}
	public void DeleteEntity(NetworkEntity entity) {

		OwnedEntities.Remove(entity);
		RelevantEntities.Remove(entity);

		int headerLength = WritePacketHeader(OutgoingReliableDataBuffer, RequestType.DeleteEntity, entity, out Span<byte> contentBuffer);
		Transport.SendPacket(OutgoingReliableDataBuffer, headerLength);
	}
}
