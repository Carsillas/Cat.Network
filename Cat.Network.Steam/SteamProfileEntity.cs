using System;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network.Steam {
	public class SteamProfileEntity : NetworkEntity {

		public NetworkProperty<ulong> Id { get; } = new NetworkProperty<ulong>();
		public NetworkProperty<string> Name { get; } = new NetworkProperty<string>();

	}
}
