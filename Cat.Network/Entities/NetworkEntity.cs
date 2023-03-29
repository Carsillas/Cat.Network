using Cat.Network.Generator;
using Cat.Network.Properties;
using Cat.Network.Serialization;
using System;

namespace Cat.Network.Entities
{

	[NetworkProperty(AccessModifier.Public, typeof(bool), "DestroyWithOwner")]
	public abstract partial class NetworkEntity : IEquatable<NetworkEntity> {
		
		public Guid NetworkID { get; internal set; }
		public bool IsOwner { get; internal set; } = true;


		internal int LastDirtyTick;


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

}
