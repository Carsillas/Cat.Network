using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using static Cat.Network.CatServer;
using static Cat.Network.SerializationUtils;

namespace Cat.Network;
internal class RemoteClient : IEntityProcessor {

	public HashSet<NetworkEntity> OwnedEntities { get; } = new HashSet<NetworkEntity>();
	public HashSet<NetworkEntity> RelevantEntities { get; } = new HashSet<NetworkEntity>();


	private byte[] OutgoingReliableDataBuffer = new byte[1_000_000];

	private ISerializationContext SerializationContext { get; }
	public ITransport Transport { get; }
	public NetworkEntity ProfileEntity { get; }

	public RemoteClient(ISerializationContext serializationContext, ITransport transport, NetworkEntity profileEntity) {
		SerializationContext = serializationContext;
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

		if (isOwner) {
			if (OwnedEntities.Add(entity)) {
				NotifyAssignedOwner(entity);
			}
			SendOutgoingRpcs(entity);
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

		if (isOwner) {
			if (OwnedEntities.Add(entity)) {
				NotifyAssignedOwner(entity);
			}
			SendOutgoingRpcs(entity);
		}

	}
	public void DeleteEntity(NetworkEntity entity) {

		OwnedEntities.Remove(entity);
		RelevantEntities.Remove(entity);

		int headerLength = WritePacketHeader(OutgoingReliableDataBuffer, RequestType.DeleteEntity, entity, out Span<byte> contentBuffer);
		Transport.SendPacket(OutgoingReliableDataBuffer, headerLength);
	}

	private void SendOutgoingRpcs(NetworkEntity entity) {

		var outgoingRpcs = SerializationContext.GetOutgoingRpcs(entity);

		if(outgoingRpcs != null) {
			foreach (byte[] rpc in outgoingRpcs) {
				const int ServerRpcHeaderLength = 17;
				const int ServertRpcContentLengthSlot = 4;
				int length = BinaryPrimitives.ReadInt32LittleEndian(new ReadOnlySpan<byte>(rpc, ServerRpcHeaderLength, 4));
				Transport.SendPacket(rpc, length + ServerRpcHeaderLength + ServertRpcContentLengthSlot);
			}
		}
	}

}
