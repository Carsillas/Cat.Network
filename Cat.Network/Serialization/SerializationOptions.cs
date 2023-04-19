using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Serialization;

public struct SerializationOptions {
	public MemberIdentifierMode MemberIdentifierMode { get; init; }
	public MemberSelectionMode MemberSelectionMode { get; init; }
	public MemberSerializationMode MemberSerializationMode { get; init; }
}

public enum TypeIdentifierMode : byte {
	None,
	AssemblyQualifiedName
}

public enum MemberIdentifierMode : byte {
	None,
	Index,
	Name,
	HashedName
}

public enum MemberSelectionMode : byte {
	Dirty,
	All
}

public enum MemberSerializationMode : byte {
	Complete,
	Partial
}