using System;
using System.Collections.Generic;
using System.Text;
using Cat.Network.Entities;
using Cat.Network.Generator;
using Cat.Network.Properties;

namespace Cat.Network.Test {


	public partial class TestProfileEntity : NetworkEntity2 {

		int NetworkProp.Health { get; set; }

		int NetworkProp.MyNewProperty { get; set; }

		void a() { 
			
		}

	}

	public partial class TestEntity : TestProfileEntity {

		int NetworkProp.Health { get; set; }
		void RPC.ModifyHealth(int amount) {
			Health += amount;
		}

	}
}