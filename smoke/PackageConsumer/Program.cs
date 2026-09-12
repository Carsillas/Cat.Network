using Cat.Network;

PlayerState player = new() { Health = 100, Name = "Ada" };
if (player.Health != 100 || player.Name != "Ada") {
	throw new InvalidOperationException("Generated property accessors did not retain their values.");
}

TypeCatalogue catalogue = new();
catalogue.Register(typeof(PlayerState));
if (!catalogue.TryFindSerializer(typeof(PlayerState), out INetworkObjectSerializer? serializer)) {
	throw new InvalidOperationException("The generated serializer was not registered.");
}

SerializationContext context = new(catalogue);
serializer.ClearDirtyState(player, context);
player.Health = 75;
BufferWriter writer = new();
serializer.Serialize(writer, player, context,
	new SerializationOptions(MemberSelectionMode.Dirty, MemberIdentificationMode.Name));

PlayerState copy = new() { Health = 100, Name = "Unchanged" };
serializer.Deserialize(copy, writer.GetWrittenSpan(), context);
if (copy.Health != 75 || copy.Name != "Unchanged") {
	throw new InvalidOperationException("Generated setters and dirty serialization did not replicate the changed property.");
}

Console.WriteLine("Package consumer passed: generated property accessors and dirty serialization on .NET " + Environment.Version);

[NetworkObject]
public partial class PlayerState : NetworkObject {
	[NetworkProperty]
	public partial int Health { get; set; }

	[NetworkProperty]
	public partial string Name { get; set; } = string.Empty;
}
