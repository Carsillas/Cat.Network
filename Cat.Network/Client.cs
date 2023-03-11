using Cat.Network.Entities;
using Cat.Network.Generator;
using Cat.Network.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Cat.Network
{

	internal delegate void RequestProcessor(ReadOnlySpan<byte> data);

    public class Client : ISerializationContext, IDisposable {



		private int Time { get; set; }
		private ITransport Transport { get; set; }
		
		public IProxyManager ProxyManager { get; }
		private IPacketSerializer PacketSerializer { get; }


		private byte[] OutgoingReliableDataBuffer = new byte[1_000_000]; // 1 MB
		

		private Dictionary<RequestType, RequestProcessor> RequestProcessor { get; } = new Dictionary<RequestType, RequestProcessor>();

		private Dictionary<Guid, NetworkEntity> Entities { get; } = new Dictionary<Guid, NetworkEntity>();
		private Dictionary<Guid, NetworkEntity> OwnedEntities { get; } = new Dictionary<Guid, NetworkEntity>();
		private HashSet<NetworkEntity> EntitiesToSpawn { get; } = new HashSet<NetworkEntity>();
		private HashSet<NetworkEntity> EntitiesToDespawn { get; } = new HashSet<NetworkEntity>();


		public Client(IProxyManager proxyManager, IPacketSerializer serializer) {
			InitializeNetworkRequestParsers();

			ProxyManager = proxyManager;
			PacketSerializer = serializer;
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

				Transport.SendPacket(new Span<byte>(OutgoingReliableDataBuffer, 0, packet.Length));
			}

			foreach (NetworkEntity entity in EntitiesToSpawn) {
				Packet packet = new PacketWriter(OutgoingReliableDataBuffer)
					.WriteRequestType(RequestType.CreateEntity)
					.WriteTarget(entity)
					//.WriteEntityData(something something something)
					.Lock();

				Transport.SendPacket(new Span<byte>(OutgoingReliableDataBuffer, 0, packet.Length));
			}

			EntitiesToSpawn.Clear();

			foreach (NetworkEntity entity in EntitiesToDespawn) {
				Packet packet = new PacketWriter(OutgoingReliableDataBuffer)
					.WriteRequestType(RequestType.DeleteEntity)
					.WriteTarget(entity)
					.Lock();

				Transport.SendPacket(new Span<byte>(OutgoingReliableDataBuffer, 0, packet.Length));
			}

			EntitiesToDespawn.Clear();
		}

		private void ProcessIncomingPackets() {
			
			Transport.ProcessPackets(Processor);

			void Processor(ReadOnlySpan<byte> bytes) {
				try {
					RequestType requestType = (RequestType)bytes[0];
					if (RequestProcessor.TryGetValue(requestType, out RequestProcessor handler)) {
						handler.Invoke(bytes.Slice(1));
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
			RequestProcessor.Add(RequestType.AssignOwner, HandleAssignOwnerRequest);
			RequestProcessor.Add(RequestType.CreateEntity, HandleCreateEntityRequest);
			RequestProcessor.Add(RequestType.UpdateEntity, HandleUpdateEntityRequest);
			RequestProcessor.Add(RequestType.DeleteEntity, HandleDeleteEntityRequest);
		}

		private void HandleCreateEntityRequest(ReadOnlySpan<byte> bytes) {

			Guid networkID = new Guid(bytes.Slice(0, 16));

			ReadOnlySpan<byte> entityData = bytes.Slice(16);

			if (Entities.TryGetValue(networkID, out NetworkEntity existingEntity)) {
				if (!OwnedEntities.ContainsKey(existingEntity.NetworkID)) {
					PacketSerializer.UpdateEntity(existingEntity, entityData);
				}
				return;
			}

			PacketSerializer.CreateEntity(networkID, entityData);
		}


		private void HandleUpdateEntityRequest(ReadOnlySpan<byte> bytes) {
			Guid networkID = new Guid(bytes.Slice(0, 16));

			if (Entities.TryGetValue(networkID, out NetworkEntity entity)) {
				if (OwnedEntities.ContainsKey(entity.NetworkID)) {
					return;
				}
				PacketSerializer.UpdateEntity(entity, bytes.Slice(16));
			}
		}

		private void HandleDeleteEntityRequest(ReadOnlySpan<byte> bytes) {
			Guid networkID = new Guid(bytes.Slice(0, 16));

			if (Entities.TryGetValue(networkID, out NetworkEntity entity)) {
				ProxyManager.OnEntityDeleted(entity);
			}

			Entities.Remove(networkID);
			OwnedEntities.Remove(networkID);
		}

		private void HandleAssignOwnerRequest(ReadOnlySpan<byte> bytes) {

			Guid entityNetworkID = new Guid(reader.ReadBytes(16));

			if (Entities.TryGetValue(entityNetworkID, out NetworkEntity entity)) {
				OwnedEntities.Add(entityNetworkID, entity);
				entity.IsOwner = true;
				ProxyManager.OnGainedOwnership(entity);
			}
		}

		public virtual void Dispose() {
			ProxyManager?.Dispose();
		}

	}
}
