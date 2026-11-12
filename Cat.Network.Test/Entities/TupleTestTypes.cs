namespace Cat.Network.Test.Entities;

// These schemas compile with both the analyzer and generator enabled by the test project.
[NetworkObject]
public partial class TupleWireState : NetworkObject {
	[NetworkProperty] public partial (int, int) Unnamed { get; set; } = (42, 17);
	[NetworkProperty] public partial (int Z, int A) Named { get; set; } = (42, 17);
	[NetworkProperty] public partial (int Item1, int Item2) DefaultNames { get; set; } = (42, 17);
	[NetworkProperty] public partial (int Z, int, int Item3) MixedNames { get; set; } = (42, 17, 9);
	[NetworkProperty] public partial (int Z, int A)? Nullable { get; set; } = (42, 17);
	[NetworkProperty] public partial (int Z, int A)? Null { get; set; }
	[NetworkProperty] public partial (int Z, (int Y, int X) Nested) Nested { get; set; } = (42, (17, 9));
	[NetworkProperty] public partial (int Z, (int Y, int X)? Nested)? NestedNullable { get; set; } = (42, (17, 9));
	[NetworkProperty] public partial (int Z, (int Y, int X)? Nested) NestedNull { get; set; } = (42, null);
	[NetworkProperty] public partial (int, int, int, int, int, int, int, int) Eight { get; set; } = (1, 2, 3, 4, 5, 6, 7, 8);
	[NetworkProperty] public partial (int Z, int Y, int X, int W, int V, int U, int T, int S, int R) Nine { get; set; } = (1, 2, 3, 4, 5, 6, 7, 8, 9);
	[NetworkProperty] public partial (int Item1, int Y, int, int, int, int, int Item7, int Item8, int R) LongMixedNames { get; set; } = (1, 2, 3, 4, 5, 6, 7, 8, 9);
	[NetworkProperty] public partial ValueTuple<int, int, int, int, int, int, int, ValueTuple<int, int>> ExplicitStorage { get; set; } = (1, 2, 3, 4, 5, 6, 7, 8, 9);
	[NetworkProperty] public partial (int Z, int Y, int X, int W, int V, int U, int T, int S, int R, int Q, int P, int O, int N, int M, int L) Fifteen { get; set; } = (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15);
	[NetworkProperty] public partial (int, int, int, int, int, int, int, (int Z, int A)?) NullableInRest { get; set; } = (1, 2, 3, 4, 5, 6, 7, (8, 9));
	[NetworkProperty] public partial TupleEnvelope Wrapped { get; set; } = new() { Pair = (42, 17), Tail = (1, 2, 3, 4, 5, 6, 7, 8, 9) };

	[NetworkCollection] public partial NetworkList<(int Z, (int Y, int X)? Nested)?> NestedItems { get; }
	[NetworkCollection] public partial NetworkDictionary<(int Z, int A), (int Z, int Y, int X, int W, int V, int U, int T, int S, int R)> Lookup { get; }
}

public struct TupleEnvelope {
	public (int Z, int A) Pair;
	public (int Z, int Y, int X, int W, int V, int U, int T, int S, int R) Tail;
}

[NetworkObject]
public partial class TupleVersion0 : NetworkObject {
	[NetworkProperty] public partial (int, int) Pair { get; set; }
	[NetworkProperty] public partial (int, int, int, int, int, int, int, int, int) Long { get; set; }
}

[NetworkObject(Version = 1)]
public partial class TupleVersion1 : NetworkObject {
	[NetworkProperty] public partial (int Z, int A) Pair { get; set; }
	[NetworkProperty] public partial (int Z, int Y, int X, int W, int V, int U, int T, int S, int R) Long { get; set; }

	[UpgradeTo(1)]
	private static void Upgrade(NetworkObjectUpgradeReader reader, NetworkObjectUpgradeWriter writer) => TupleUpgrade.Copy(reader, writer);
}

[NetworkObject(Version = 2)]
public partial class TupleVersion2 : NetworkObject {
	[NetworkProperty] public partial (int Left, int Right) Pair { get; set; }
	[NetworkProperty] public partial (int A, int B, int C, int D, int E, int F, int G, int H, int I) Long { get; set; }

	[UpgradeTo(1)]
	private static void UpgradeTo1(NetworkObjectUpgradeReader reader, NetworkObjectUpgradeWriter writer) => TupleUpgrade.Copy(reader, writer);
	[UpgradeTo(2)]
	private static void UpgradeTo2(NetworkObjectUpgradeReader reader, NetworkObjectUpgradeWriter writer) => TupleUpgrade.Copy(reader, writer);
}

internal static class TupleUpgrade {
	public static void Copy(NetworkObjectUpgradeReader reader, NetworkObjectUpgradeWriter writer) {
		writer.Write("Pair", reader.Get<(int, int)>("Pair"));
		writer.Write("Long", reader.Get<(int, int, int, int, int, int, int, int, int)>("Long"));
	}
}

[NetworkObject]
public partial class TupleMessageState : NetworkEntity {
	[RPC]
	public partial void ApplyTuples((int Z, int A) pair, (int Z, int Y, int X, int W, int V, int U, int T, int S, int R) longValue, TupleEnvelope wrapped, (int Z, (int Y, int X)? Nested)? optional);

	[Broadcast]
	public partial void PublishTuples((int Z, int A) pair, (int Z, int Y, int X, int W, int V, int U, int T, int S, int R) longValue, TupleEnvelope wrapped, (int Z, (int Y, int X)? Nested)? optional);
}
