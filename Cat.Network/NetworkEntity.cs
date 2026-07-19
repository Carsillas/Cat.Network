namespace Cat.Network;

using System.Collections.Immutable;

public abstract partial class NetworkEntity : NetworkObject {

	public Guid Id { get; internal set; }
	
	protected static ImmutableArray<NetworkPropertyInfo> Properties { get; } = [];
	
	
}