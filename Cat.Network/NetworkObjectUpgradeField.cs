namespace Cat.Network;

public readonly record struct NetworkObjectUpgradeField(string Name, ReadOnlyMemory<byte> Value);
