using static Cat.Network.Generator.Utils;

namespace Cat.Network.Generator {
	public class NetworkDataObjectInterfaceImplementationGenerator : NetworkSerializableInterfaceImplementationGenerator {
		protected override string SerializableTypeKind { get; } = "record";
		protected override string InterfaceFQN { get; } = NetworkDataObjectInterfaceFQN;
	}
}