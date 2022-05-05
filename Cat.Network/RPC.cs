using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cat.Network {
	public enum RPCInvokeSite {
		Owner
	}


	[AttributeUsage(AttributeTargets.Method)]
	public class RPC : Attribute {
		internal RPCInvokeSite InvokeSite { get; }

		public RPC(RPCInvokeSite invokeSite) {
			InvokeSite = invokeSite;
		}

	}

}
