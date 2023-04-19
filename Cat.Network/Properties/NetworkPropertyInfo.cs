using Cat.Network.Entities;
using Cat.Network.Generator;
using Cat.Network.Serialization;
using System;
using System.Buffers.Binary;
using System.Security.Principal;

namespace Cat.Network.Properties {

	public struct NetworkPropertyInfo {
		public int Index { get; init; }
		public string Name { get; init; }
		public bool Dirty { get; set; }
	}

}
