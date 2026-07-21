namespace Cat.Network;

public enum EntityMessageKind : byte {
	Create,
	Delete,
	Update,
	Rpc,
	Broadcast
}