using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Cat.Network.Entities;
using Cat.Network.Generator;
using Cat.Network.Properties;

namespace Cat.Network.Test {

	public partial class TestProfileEntity : NetworkEntity { }

	public partial class TestEntity : NetworkEntity {

		int NetworkProp.Health { get; set; }

		void RPC.ModifyHealth(int amount) {
			Health += amount;
		}

	}
}