namespace Cat.Network;

public enum ProfileMessageKind : byte {
	Assign,
	Create,
	Update,
	Delete,
	UpdateRequest,
	Synchronize
}
