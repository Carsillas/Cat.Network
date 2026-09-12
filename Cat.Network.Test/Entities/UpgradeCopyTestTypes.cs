namespace Cat.Network.Test.Entities;

[NetworkObject(Version = 1)]
public partial class LegacyUpgradeCollectionsState : NetworkObject {
	[NetworkProperty]
	public partial int Marker { get; set; }

	[NetworkProperty]
	public partial int? OptionalValue { get; set; }

	[NetworkProperty]
	public partial DirtyChildState? Child { get; set; }

	[NetworkCollection]
	public partial NetworkList<int> Values { get; }

	[NetworkCollection]
	public partial NetworkDictionary<int, string> Labels { get; }

	[NetworkCollection]
	public partial NetworkList<int?> NullableValues { get; }

	[NetworkCollection]
	public partial NetworkList<UpgradeNestedCollectionState?> Children { get; }

	[NetworkCollection]
	public partial NetworkDictionary<int, UpgradeNestedCollectionState?> ChildLookup { get; }
}

[NetworkObject(Version = 2)]
public partial class RenamedUpgradeCollectionsState : NetworkObject {
	[NetworkProperty]
	public partial int Marker { get; set; }

	[NetworkProperty]
	public partial int? OptionalCount { get; set; }

	[NetworkProperty]
	public partial DirtyChildState? Detail { get; set; }

	[NetworkCollection]
	public partial NetworkList<int> Items { get; }

	[NetworkCollection]
	public partial NetworkDictionary<int, string> Names { get; }

	[NetworkCollection]
	public partial NetworkList<int?> OptionalItems { get; }

	[NetworkCollection]
	public partial NetworkList<UpgradeNestedCollectionState?> NestedItems { get; }

	[NetworkCollection]
	public partial NetworkDictionary<int, UpgradeNestedCollectionState?> NestedLookup { get; }

	[UpgradeTo(2)]
	internal static void UpgradeToVersion2(NetworkObjectUpgradeReader reader, NetworkObjectUpgradeWriter writer) {
		writer.CopyExcept("Values", "Labels", "OptionalValue", "Child", "NullableValues", "Children", "ChildLookup");
		writer.Copy("Values", "Items");
		writer.Copy("Labels", "Names");
		writer.Copy("OptionalValue", "OptionalCount");
		writer.Copy("Child", "Detail");
		writer.Copy("NullableValues", "OptionalItems");
		writer.Copy("Children", "NestedItems");
		writer.Copy("ChildLookup", "NestedLookup");
	}
}

[NetworkObject(Version = 2)]
public partial class UnchangedUpgradeCollectionsState : NetworkObject {
	[NetworkProperty]
	public partial int Marker { get; set; }

	[NetworkCollection]
	public partial NetworkList<int> Values { get; }

	[NetworkCollection]
	public partial NetworkDictionary<int, string> Labels { get; }

	[UpgradeTo(2)]
	internal static void UpgradeToVersion2(NetworkObjectUpgradeReader reader, NetworkObjectUpgradeWriter writer) {
		writer.CopyExcept();
	}
}

[NetworkObject]
public partial class UpgradeNestedCollectionState : NetworkObject {
	[NetworkCollection]
	public partial NetworkList<int?> Values { get; }

	[NetworkCollection]
	public partial NetworkDictionary<int, string> Labels { get; }
}
