using System;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network {
	internal enum RequestType : byte {
		CreateEntity = 1,
		UpdateEntity = 2,
		DeleteEntity = 3,
		RPC = 4,
		AssignOwner = 5
	}
}
