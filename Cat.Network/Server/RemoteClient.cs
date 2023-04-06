using Cat.Network.Entities;
using Cat.Network.Generator;
using Cat.Network.Properties;
using Cat.Network.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Cat.Network.Serialization.SerializationUtils;

namespace Cat.Network.Server;
internal class RemoteClient : IEntityProcessor {

	public HashSet<NetworkEntity> OwnedEntities { get; } = new HashSet<NetworkEntity>();
	public HashSet<NetworkEntity> RelevantEntities { get; } = new HashSet<NetworkEntity>();
	IReadOnlySet<NetworkEntity> IEntityProcessor.RelevantEntities => RelevantEntities;


	private byte[] OutgoingReliableDataBuffer = new byte[1_000_000];


	public IPacketSerializer Serializer { get; }
	public ITransport Transport { get; }
	public NetworkEntity ProfileEntity { get; }


	public RemoteClient(IPacketSerializer serializer, ITransport transport, NetworkEntity profileEntity) {
		Serializer = serializer;
		Transport = transport;
		ProfileEntity = profileEntity;
	}



	public void NotifyAssignedOwner(NetworkEntity entity) {
		WritePacketHeader(OutgoingReliableDataBuffer, RequestType.AssignOwner, entity.NetworkID);
		Transport.SendPacket(OutgoingReliableDataBuffer, HeaderLength);
	}

	public void CreateEntity(NetworkEntity entity, bool isOwner) {
		RelevantEntities.Add(entity);

		WritePacketHeader(OutgoingReliableDataBuffer, RequestType.CreateEntity, entity.NetworkID);
		int contentLength = Serializer.WriteCreateEntity(entity, GetContentSpan(OutgoingReliableDataBuffer));

		Transport.SendPacket(OutgoingReliableDataBuffer, HeaderLength + contentLength);

		if (isOwner && OwnedEntities.Add(entity)) {
			NotifyAssignedOwner(entity);
		}
	}

	public void UpdateEntity(NetworkEntity entity, bool isOwner) {

		bool isDirty = ((INetworkEntity)entity).SerializationContext.Time == entity.LastDirtyTick;

		if (isDirty) {
			WritePacketHeader(OutgoingReliableDataBuffer, RequestType.UpdateEntity, entity.NetworkID);
			int contentLength = Serializer.WriteUpdateEntity(entity, GetContentSpan(OutgoingReliableDataBuffer));

			Transport.SendPacket(OutgoingReliableDataBuffer, HeaderLength + contentLength);
		} else if(entity.LastDirtyTick > -1) {
			entity.LastDirtyTick = -1;

			foreach(NetworkProperty prop in ((INetworkEntity)entity).NetworkProperties) {
				prop.Clean();
			}
		}

		if (isOwner && OwnedEntities.Add(entity)) {
			NotifyAssignedOwner(entity);
		}

	}
	public void DeleteEntity(NetworkEntity entity) {

		OwnedEntities.Remove(entity);
		RelevantEntities.Remove(entity);

		WritePacketHeader(OutgoingReliableDataBuffer, RequestType.DeleteEntity, entity.NetworkID);
		Transport.SendPacket(OutgoingReliableDataBuffer, HeaderLength);
	}
}
