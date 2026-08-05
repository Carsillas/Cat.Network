namespace Cat.Network;

public enum EntityMessageKind : byte {
	AssignOwner,
	RequestOwnershipTransfer,
	Create,
	Update,
	Delete,
	Rpc,
	Broadcast
}
