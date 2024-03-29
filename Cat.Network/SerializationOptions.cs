﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network;

public struct SerializationOptions {
	public MemberIdentifierMode MemberIdentifierMode { get; set; }
	public MemberSelectionMode MemberSelectionMode { get; set; }
	public MemberSerializationMode MemberSerializationMode { get; set; }
}

public enum MemberIdentifierMode : byte {
	Index,
	Name
}

public enum MemberSelectionMode : byte {
	Dirty,
	All
}

public enum MemberSerializationMode : byte {
	Complete,
	Partial
}