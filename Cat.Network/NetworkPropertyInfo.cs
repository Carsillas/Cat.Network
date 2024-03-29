﻿using System;
using System.Buffers.Binary;
using System.Security.Principal;

namespace Cat.Network;

public struct NetworkPropertyInfo {
	public int Index { get; init; }
	public string Name { get; init; }
	public int LastSetTick { get; set; }
	public int LastUpdateTick { get; set; }
}
