using Cat.Network.Entities;
using Cat.Network.Generator;
using Cat.Network.Serialization;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using static Cat.Network.Serialization.SerializationUtils;

namespace Cat.Network
{
	public class Client : ISerializationContext, IDisposable {

		private int Time { get; set; }
		private ITransport Transport { get; set; }
		
		public IProxyManager ProxyManager { get; }
		private IPacketSerializer Serializer { get; }


		private byte[] OutgoingReliableDataBuffer = new byte[1_000_000];
		

		private Dictionary<RequestType, ClientRequestProcessor> RequestProcessors { get; } = new Dictionary<RequestType, ClientRequestProcessor>();

		private Dictionary<Guid, NetworkEntity> Entities { get; } = new Dictionary<Guid, NetworkEntity>();
		private Dictionary<Guid, NetworkEntity> OwnedEntities { get; } = new Dictionary<Guid, NetworkEntity>();
		private HashSet<NetworkEntity> EntitiesToSpawn { get; } = new HashSet<NetworkEntity>();
		private HashSet<NetworkEntity> EntitiesToDespawn { get; } = new HashSet<NetworkEntity>();


		public Client(IProxyManager proxyManager, IPacketSerializer serializer) {
			InitializeNetworkRequestParsers();

			ProxyManager = proxyManager;
			Serializer = serializer;
		}

		public void Connect(ITransport serverTransport) {
			Transport = serverTransport;
		}

		public void Spawn(NetworkEntity entity) {
			entity.NetworkID = Guid.NewGuid();

			Entities.Add(entity.NetworkID, entity);
			EntitiesToSpawn.Add(entity);
			OwnedEntities.Add(entity.NetworkID, entity);

			ProxyManager.OnEntityCreated(entity);
			entity.IsOwner = true;
			ProxyManager.OnGainedOwnership(entity);

		}

		public void Despawn(NetworkEntity entity) {
			ProxyManager.OnEntityDeleted(entity);
			Entities.Remove(entity.NetworkID);
			OwnedEntities.Remove(entity.NetworkID);

			if (!EntitiesToSpawn.Remove(entity)) {
				EntitiesToDespawn.Add(entity);
			}
		}

		public bool TryGetEntityByNetworkID(Guid NetworkID, out NetworkEntity entity) {
			return Entities.TryGetValue(NetworkID, out entity);
		}

		protected virtual void PreTick() {

		}

		public void Tick() {
			PreTick();

			Time++;

			PostTick();
		}

		private void PostTick() {

			if (Transport == null) {
				return;
			}

			ProcessIncomingPackets();

			foreach (NetworkEntity entity in OwnedEntities.Values) {
				if (EntitiesToSpawn.Contains(entity) || EntitiesToDespawn.Contains(entity)) {
					continue;
				}

				Packet packet = new PacketWriter(OutgoingReliableDataBuffer)
					.WriteRequestType(RequestType.UpdateEntity)
					.WriteTarget(entity)
					//.WriteEntityData(something something something)
					.Lock();

				Transport.SendPacket(OutgoingReliableDataBuffer, packet.Length);
			}

			foreach (NetworkEntity entity in EntitiesToSpawn) {
				Packet packet = new PacketWriter(OutgoingReliableDataBuffer)
					.WriteRequestType(RequestType.CreateEntity)
					.WriteTarget(entity)
					//.WriteEntityData(something something something)
					.Lock();

				Transport.SendPacket(OutgoingReliableDataBuffer, packet.Length);
			}

			EntitiesToSpawn.Clear();

			foreach (NetworkEntity entity in EntitiesToDespawn) {
				Packet packet = new PacketWriter(OutgoingReliableDataBuffer)
					.WriteRequestType(RequestType.DeleteEntity)
					.WriteTarget(entity)
					.Lock();

				Transport.SendPacket(OutgoingReliableDataBuffer, packet.Length);
			}

			EntitiesToDespawn.Clear();
		}

		private void ProcessIncomingPackets() {
			
			Transport.ProcessPackets(Processor);

			void Processor(ReadOnlySpan<byte> bytes) {
				try {
					RequestType requestType = (RequestType)bytes[0];
					if (RequestProcessors.TryGetValue(requestType, out ClientRequestProcessor handler)) {
						ExtractPacketHeader(bytes, out Guid networkID, out ReadOnlySpan<byte> content);
						handler.Invoke(networkID, content);
					} else {
						Console.WriteLine($"Unknown network request type: {requestType}");
					}
				} catch (Exception e) {
					Console.Error.WriteLine(e.Message);
					Console.Error.WriteLine(e.StackTrace);
				}
			}

		}

		private void InitializeNetworkRequestParsers() {
			RequestProcessors.Add(RequestType.AssignOwner, HandleAssignOwnerRequest);
			RequestProcessors.Add(RequestType.CreateEntity, HandleCreateEntityRequest);
			RequestProcessors.Add(RequestType.UpdateEntity, HandleUpdateEntityRequest);
			RequestProcessors.Add(RequestType.DeleteEntity, HandleDeleteEntityRequest);
		}

		private void HandleCreateEntityRequest(Guid networkID, ReadOnlySpan<byte> content) {
			if (Entities.TryGetValue(networkID, out NetworkEntity existingEntity)) {
				if (!OwnedEntities.ContainsKey(existingEntity.NetworkID)) {
					Serializer.UpdateEntity(existingEntity, content);
				}
				return;
			}

			Serializer.CreateEntity(networkID, content);
		}

		private void HandleUpdateEntityRequest(Guid networkID, ReadOnlySpan<byte> content) {
			if (Entities.TryGetValue(networkID, out NetworkEntity entity)) {
				if (OwnedEntities.ContainsKey(entity.NetworkID)) {
					return;
				}
				Serializer.UpdateEntity(entity, content);
			}
		}

		private void HandleDeleteEntityRequest(Guid networkID, ReadOnlySpan<byte> content) {
			if (Entities.TryGetValue(networkID, out NetworkEntity entity)) {
				ProxyManager.OnEntityDeleted(entity);
			}

			Entities.Remove(networkID);
			OwnedEntities.Remove(networkID);
		}

		private void HandleAssignOwnerRequest(Guid networkID, ReadOnlySpan<byte> content) {
			if (Entities.TryGetValue(networkID, out NetworkEntity entity)) {
				OwnedEntities.Add(networkID, entity);
				entity.IsOwner = true;
				ProxyManager.OnGainedOwnership(entity);
			}
		}

		public virtual void Dispose() {
			ProxyManager?.Dispose();
		}

	}
}
