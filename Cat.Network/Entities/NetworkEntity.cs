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
		
		private NetworkProperty[] NetworkProperties;
		NetworkProperty[] INetworkEntity.NetworkProperties { get => NetworkProperties; set => NetworkProperties = value; }

		public NetworkEntity() {
			((INetworkEntity)this).Initialize();
		}

		public bool Equals(NetworkEntity other) {
			return NetworkID == other.NetworkID;
		}

	}


	public partial class NetworkEntity2 : NetworkEntity {

		int NetworkProp.Test { get; set; }

	}

}
