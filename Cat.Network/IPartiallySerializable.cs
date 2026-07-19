using System.Collections.Immutable;

namespace Cat.Network;

public interface IPartiallySerializable {
	
	ImmutableArray<NetworkPropertyInfo> Properties { get; }

	void Initialize();

}