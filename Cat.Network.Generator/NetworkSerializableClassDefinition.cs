using System.Collections.Immutable;

namespace Cat.Network.Generator {
	public class NetworkSerializableClassDefinition {
		public string Name { get; set; }
		public string BaseTypeFQN { get; set; }
		public string Namespace { get; set; }
		public string MetadataName { get; set; }
		public ImmutableArray<NetworkPropertyData> NetworkProperties { get; set; }
	}
}