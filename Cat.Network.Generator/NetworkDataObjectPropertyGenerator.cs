
using static Cat.Network.Generator.Utils;

// @formatter:csharp_max_line_length 400

namespace Cat.Network.Generator {
	public class NetworkDataObjectPropertyGenerator : NetworkSerializablePropertyGenerator {
		protected override string SerializableTypeKind { get; } = "record";
		
		protected override string BaseFQN { get; } = NetworkDataObjectFQN;
		protected override string InterfaceFQN { get; } = NetworkDataObjectInterfaceFQN;

		protected override void GenerateGetter(ScopedStringWriter writer, int propertyIndex, NetworkPropertyData data) {
			writer.AppendLine($"get => (({NetworkPropertyPrefix})this).{data.Name};");
		}

		protected override void GenerateSetter(ScopedStringWriter writer, int propertyIndex, NetworkPropertyData data) {
			using (writer.EnterScope("set")) {
				writer.AppendBlock($@"
					{NetworkDataObjectInterfaceFQN} iNetworkDataObject = this;

					if (iNetworkDataObject.Anchor != null) {{
						iNetworkDataObject.Anchor.LastDirtyTick = iNetworkDataObject.SerializationContext?.Time ?? 0;
					}}

					ref {NetworkPropertyInfoFQN} networkPropertyInfo = ref iNetworkDataObject.NetworkProperties[{propertyIndex}];
					networkPropertyInfo.LastSetTick = iNetworkDataObject.SerializationContext?.Time ?? 0;

					if (iNetworkDataObject.Parent != null) {{
						ref {NetworkPropertyInfoFQN} parentNetworkPropertyInfo = iNetworkDataObject.Parent.NetworkProperties[iNetworkDataObject.PropertyIndex];
						parentNetworkPropertyInfo.LastModifiedTick = iNetworkDataObject.SerializationContext?.Time ?? 0;
					}}


					var oldValue = (({NetworkPropertyPrefix})this).{data.Name};
					(({NetworkPropertyPrefix})this).{data.Name} = value;
				");
			}
		}
		
	}
}