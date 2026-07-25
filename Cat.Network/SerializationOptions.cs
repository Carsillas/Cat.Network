namespace Cat.Network;

public readonly struct SerializationOptions(MemberSelectionMode memberSelectionMode, MemberIdentificationMode memberIdentificationMode) {
	public MemberIdentificationMode MemberIdentificationMode { get; } = memberIdentificationMode;
	public MemberSelectionMode MemberSelectionMode { get; } = memberSelectionMode;
}
