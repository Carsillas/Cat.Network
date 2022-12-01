using System;

namespace Cat.Network {
	public partial class NetworkEntity {
		public Guid NetworkID { get; internal set; }
		internal NetworkEntitySerializer Serializer { get; }

		public NetworkProperty<bool> DestroyWithOwner { get; } = new NetworkProperty<bool>();

		public bool IsOwner { get; internal set; }

		public NetworkEntity() {
			Serializer = new NetworkEntitySerializer(this);
		}

	}
}
