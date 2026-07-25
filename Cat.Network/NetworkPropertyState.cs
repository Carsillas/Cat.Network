namespace Cat.Network;

[Flags]
public enum NetworkPropertyState : byte {
	Unchanged = 0,
	Modified = 1 << 0,
	Replaced = 1 << 1
}