namespace Cat.Network;

using System.Collections.Immutable;

public abstract partial class NetworkEntity : IPartiallySerializable {

	protected static ImmutableArray<NetworkPropertyInfo> Properties { get; } = [];

	ImmutableArray<NetworkPropertyInfo> IPartiallySerializable.Properties => Properties;

	protected NetworkEntity() {
		((IPartiallySerializable)this).Initialize();
	}
	
	public void Initialize() {
		
	}
	
}