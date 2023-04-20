﻿using Cat.Network.Generator;
using Cat.Network.Properties;
using Cat.Network.Serialization;
using System;

namespace Cat.Network.Entities
{

	public abstract partial class NetworkEntity : IEquatable<NetworkEntity> {
		
		public Guid NetworkID { get; internal set; }
		public bool IsOwner { get; internal set; } = true;

		bool NetworkProperty.DestroyWithOwner { get; set; }

		ISerializationContext INetworkEntity.SerializationContext { get; set; }
		NetworkPropertyInfo[] INetworkEntity.NetworkProperties { get; set; }
		int INetworkEntity.LastDirtyTick { get; set; }

		public NetworkEntity() {
			((INetworkEntity)this).Initialize();
		}

		public bool Equals(NetworkEntity other) {
			return NetworkID == other.NetworkID;
		}

	}

}
