using Cat.Network.Generator;
using Cat.Network.Properties;
using Cat.Network.Serialization;
using System;

namespace Cat.Network.Entities
{

	public abstract partial class NetworkEntity : IEquatable<NetworkEntity> {
		
		public Guid NetworkID { get; internal set; }
		public bool IsOwner { get; internal set; } = true;


		internal int LastDirtyTick;


		bool NetworkProp.DestroyWithOwner { get; set; }


		ISerializationContext INetworkEntity.SerializationContext { get; set; }
		
		NetworkProperty[] INetworkEntity.NetworkProperties { get; set; }

		public NetworkEntity() {
			((INetworkEntity)this).Initialize();
		}

		public bool Equals(NetworkEntity other) {
			return NetworkID == other.NetworkID;
		}

	}

}
