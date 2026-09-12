using Cat.Network;

PlayerState original = new() { Health = 42 };
PlayerState clone = original.Clone();

if (ReferenceEquals(original, clone) || clone.Health != 42) {
	throw new InvalidOperationException("The generated Clone() must copy the network property into a new instance.");
}

clone.Health = 7;
if (original.Health != 42 || clone.Health != 7) {
	throw new InvalidOperationException("Generated property accessors must preserve independent instance state.");
}

Console.WriteLine("Source references generated working network properties and Clone().");

[NetworkObject]
public partial class PlayerState : NetworkObject {
	[NetworkProperty]
	public partial int Health { get; set; }
}

#if SOURCE_REFERENCE_SMOKE_INVALID_DECLARATION
// Valid C# without the Cat.Network analyzer: only CN0001 should reject this declaration.
public sealed class MissingNetworkObjectAttribute : NetworkObject {
	public override NetworkObject Clone() => new MissingNetworkObjectAttribute();
}
#endif
