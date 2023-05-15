﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
namespace Cat.Network.Test; 

public partial class TestProfileEntity : NetworkEntity { }

public partial class TestEntity : NetworkEntity {

	int NetworkProperty.Health { get; set; }

	void RPC.ModifyHealth(int amount) {
		Health += amount;
	}

}