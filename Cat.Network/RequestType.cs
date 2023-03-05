using System;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network {
	internal enum RequestType : byte {
		AssignOwner = 0,
		CreateEntity = 1,
		UpdateEntity = 2,
		DeleteEntity = 3
	}

}
