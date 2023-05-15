using System;
using System.Buffers.Binary;
using System.Security.Principal;

namespace Cat.Network;

public struct NetworkPropertyInfo {
	public int Index { get; init; }
	public string Name { get; init; }
	public bool Dirty { get; set; }
}
