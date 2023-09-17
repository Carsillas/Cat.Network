
using static Cat.Network.Generator.Utils;

// @formatter:csharp_max_line_length 400

namespace Cat.Network.Generator {
	public class NetworkDataObjectPropertyGenerator : NetworkSerializablePropertyGenerator {
		
		protected override void GenerateGetter(ScopedStringWriter writer, int propertyIndex, NetworkPropertyData data) {
			writer.AppendLine($"get => (({NetworkPropertyPrefix})this).{data.Name};");
		}

		protected override void GenerateSetter(ScopedStringWriter writer, int propertyIndex, NetworkPropertyData data) {
			using (writer.EnterScope("set")) {
				writer.AppendBlock($@"
					{NetworkDataObjectInterfaceFQN} iNetworkDataObject = this;
					iNetworkDataObject.Anchor.LastDirtyTick = iNetworkDataObject.SerializationContext?.Time ?? 0;

					ref {NetworkPropertyInfoFQN} networkPropertyInfo = ref iNetworkDataObject.NetworkProperties[{propertyIndex}];
					networkPropertyInfo.LastSetTick = iEntity.SerializationContext?.Time ?? 0;

					ref {NetworkPropertyInfoFQN} parentNetworkPropertyInfo = iNetworkDataObject.Anchor.NetworkProperties[iNetworkDataObject.PropertyIndex];
					parentNetworkPropertyInfo.LastModifiedTick = iEntity.SerializationContext?.Time ?? 0;

					var oldValue = (({NetworkPropertyPrefix})this).{data.Name};
					(({NetworkPropertyPrefix})this).{data.Name} = value;
				");
			}
		}
		
	}
}