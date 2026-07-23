namespace Cat.Network;

public enum EntityMessageKind : byte {
	Create,
	Update,
	Delete,
	Rpc,
	Broadcast
}