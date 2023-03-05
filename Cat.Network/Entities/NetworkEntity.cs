using Cat.Network.Generator;
using Cat.Network.Properties;
using System;

namespace Cat.Network.Entities
{

	[NetworkProperty(AccessModifier.Public, typeof(bool), "DestroyWithOwner")]
	public abstract partial class NetworkEntity : IEquatable<NetworkEntity> {
		
		public Guid NetworkID { get; internal set; }
		public bool IsOwner { get; internal set; }


		private NetworkProperty[] NetworkProperties;
		NetworkProperty[] INetworkEntityInitializer.NetworkProperties { get => NetworkProperties; set => NetworkProperties = value; }


		public NetworkEntity() {
			((INetworkEntityInitializer)this).Initialize();


		}

		public bool Equals(NetworkEntity other) {
			return NetworkID == other.NetworkID;
		}

	}

}
