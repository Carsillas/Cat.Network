using System;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network.Steam
{
    public partial class SteamProfileEntity : NetworkEntity {

		ulong NetworkProperty.Id { get; set; }
		string NetworkProperty.Name { get; set; }

	}
}
