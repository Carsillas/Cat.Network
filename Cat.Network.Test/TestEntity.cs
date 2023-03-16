using System;
using System.Collections.Generic;
using System.Text;
using Cat.Network.Entities;
using Cat.Network.Generator;
using Cat.Network.Properties;

namespace Cat.Network.Test {

	public class TestProfileEntity : NetworkEntity {


	}

	[NetworkProperty(AccessModifier.Public, typeof(int), "TestInt")]
	public partial class TestEntity : NetworkEntity {


	}
}
