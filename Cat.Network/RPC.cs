using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cat.Network {

	[AttributeUsage(AttributeTargets.Method)]
	public class RPC : Attribute { }

}
