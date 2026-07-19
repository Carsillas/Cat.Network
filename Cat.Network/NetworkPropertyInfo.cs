using System.Collections.Immutable;

namespace Cat.Network;

public readonly struct NetworkPropertyInfo {
	public int Index { get; init; }
	public string Name { get; init; }
	public ImmutableArray<byte> EncodedName { get; init; }
}
