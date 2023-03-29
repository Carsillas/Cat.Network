﻿using Cat.Network.Entities;
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
	public class CatClient : ISerializationContext, IDisposable {

		private ITransport Transport { get; set; }
		
		public IProxyManager ProxyManager { get; }
		private IPacketSerializer Serializer { get; }

		bool ISerializationContext.DeserializeDirtiesProperty => false;
		public int Time { get; private set; }


		private byte[] OutgoingReliableDataBuffer = new byte[1_000_000];
		

		private Dictionary<RequestType, ClientRequestProcessor> RequestProcessors { get; } = new Dictionary<RequestType, ClientRequestProcessor>();

		private Dictionary<Guid, NetworkEntity> Entities { get; } = new Dictionary<Guid, NetworkEntity>();
		private HashSet<NetworkEntity> EntitiesToSpawn { get; } = new HashSet<NetworkEntity>();
		private HashSet<NetworkEntity> EntitiesToDespawn { get; } = new HashSet<NetworkEntity>();

		public CatClient(IProxyManager proxyManager, IPacketSerializer serializer) {
			InitializeNetworkRequestParsers();

			ProxyManager = proxyManager;
			Serializer = serializer;
		}

		public void Connect(ITransport serverTransport) {
			Transport = serverTransport;
		}

		public void Spawn(NetworkEntity entity) {
			entity.NetworkID = Guid.NewGuid();
			INetworkEntity iEntity = entity;
			iEntity.SerializationContext = this;

			Entities.Add(entity.NetworkID, entity);
			EntitiesToSpawn.Add(entity);

			ProxyManager.OnEntityCreated(entity);
			ProxyManager.OnGainedOwnership(entity);

		}

		public void Despawn(NetworkEntity entity) {
			ProxyManager.OnEntityDeleted(entity);
			Entities.Remove(entity.NetworkID);

			INetworkEntity iEntity = entity;
			iEntity.SerializationContext = null;

			if (!EntitiesToSpawn.Remove(entity)) {
				EntitiesToDespawn.Add(entity);
			}
		}

		public bool TryGetEntityByNetworkID(Guid NetworkID, out NetworkEntity entity) {
			return Entities.TryGetValue(NetworkID, out entity);
		}

		protected virtual void Execute() {

		}

		public void Tick() {
			ProcessIncomingPackets();
			Execute();
			ProcessOutgoingPackets();
			Time++;
		}

		private void ProcessOutgoingPackets() {

			foreach (NetworkEntity entity in Entities.Values) {
				if (!entity.IsOwner || entity.LastDirtyTick < Time || EntitiesToSpawn.Contains(entity) || EntitiesToDespawn.Contains(entity)) {
					continue;
				}
				 
				WritePacketHeader(OutgoingReliableDataBuffer, RequestType.UpdateEntity, entity.NetworkID);
				int contentLength = Serializer.WriteUpdateEntity(entity, GetContentSpan(OutgoingReliableDataBuffer));

				Transport.SendPacket(OutgoingReliableDataBuffer, HeaderLength + contentLength);

				INetworkEntity iEntity = entity;
				foreach(Properties.NetworkProperty prop in iEntity.NetworkProperties) {
					prop.Dirty = false;
				}
			}

			foreach (NetworkEntity entity in EntitiesToSpawn) {
				WritePacketHeader(OutgoingReliableDataBuffer, RequestType.CreateEntity, entity.NetworkID);
				int contentLength = Serializer.WriteCreateEntity(entity, GetContentSpan(OutgoingReliableDataBuffer));

				Transport.SendPacket(OutgoingReliableDataBuffer, HeaderLength + contentLength);
			}

			EntitiesToSpawn.Clear();

			foreach (NetworkEntity entity in EntitiesToDespawn) {
				WritePacketHeader(OutgoingReliableDataBuffer, RequestType.DeleteEntity, entity.NetworkID);

				Transport.SendPacket(OutgoingReliableDataBuffer, HeaderLength);
			}

			EntitiesToDespawn.Clear();
		}

		private void ProcessIncomingPackets() {
			
			Transport.ProcessPackets(Processor);

			void Processor(ReadOnlySpan<byte> bytes) {
				try {
					ExtractPacketHeader(bytes, out RequestType requestType, out Guid networkID, out ReadOnlySpan<byte> content);
					if (RequestProcessors.TryGetValue(requestType, out ClientRequestProcessor handler)) {
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
				if (!existingEntity.IsOwner) {
					Serializer.ReadUpdateEntity(existingEntity, content);
				}
				return;
			}

			NetworkEntity entity = Serializer.ReadCreateEntity(networkID, content);
			entity.IsOwner = false;
			INetworkEntity iEntity = entity;
			iEntity.SerializationContext = this;

			Entities[networkID] = entity;
		}

		private void HandleUpdateEntityRequest(Guid networkID, ReadOnlySpan<byte> content) {
			if (Entities.TryGetValue(networkID, out NetworkEntity entity)) {
				if (entity.IsOwner) {
					return;
				}
				Serializer.ReadUpdateEntity(entity, content);
			}
		}

		private void HandleDeleteEntityRequest(Guid networkID, ReadOnlySpan<byte> content) {
			if (Entities.TryGetValue(networkID, out NetworkEntity entity)) {
				INetworkEntity iEntity = entity;
				iEntity.SerializationContext = null;
				ProxyManager.OnEntityDeleted(entity);
			}

			Entities.Remove(networkID);
		}

		private void HandleAssignOwnerRequest(Guid networkID, ReadOnlySpan<byte> content) {
			if (Entities.TryGetValue(networkID, out NetworkEntity entity)) {
				entity.IsOwner = true;
				ProxyManager.OnGainedOwnership(entity);
			}
		}

		public virtual void Dispose() {
			ProxyManager?.Dispose();
		}

	}
}
