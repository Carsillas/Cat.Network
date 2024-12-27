
using static Cat.Network.Generator.Utils;

// @formatter:csharp_max_line_length 400

namespace Cat.Network.Generator {
	public class NetworkDataObjectPropertyGenerator : NetworkSerializablePropertyGenerator {
		protected override string SerializableTypeKind { get; } = "record";
		
		protected override string BaseFQN { get; } = NetworkDataObjectFQN;
		protected override string InterfaceFQN { get; } = NetworkDataObjectInterfaceFQN;

		protected override void GenerateAdditionalPropertyDefinition(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition, NetworkPropertyData data) {
			if (data.ExposeEvent) {
				writer.AppendLine($"public event {NetworkPropertyChangedEventFQN}<{classDefinition.Name}, {data.TypeInfo.FullyQualifiedTypeName}> {data.Name}Changed;");
			}
		}

		private void GenerateEventInvocation(ScopedStringWriter writer, int propertyIndex, NetworkPropertyData data) {
			writer.AppendBlock($@"
				{NetworkPropertyChangedEventArgsFQN}<{data.TypeInfo.FullyQualifiedTypeName}> args = new {NetworkPropertyChangedEventArgsFQN}<{data.TypeInfo.FullyQualifiedTypeName}> {{
					PreviousValue = oldValue,
					CurrentValue = (({NetworkPropertyPrefix})this).{data.Name}
				}};

				if (!System.Collections.Generic.EqualityComparer<{data.TypeInfo.FullyQualifiedTypeName}>.Default.Equals(args.PreviousValue, args.CurrentValue)) {{
					{data.Name}Changed?.Invoke(this, args);
				}}
			");
		}
		
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
						if (iNetworkDataObject.Collection == null) {{
							ref {NetworkPropertyInfoFQN} parentNetworkPropertyInfo = ref iNetworkDataObject.Parent.NetworkProperties[iNetworkDataObject.PropertyIndex];
							parentNetworkPropertyInfo.LastUpdateTick = iNetworkDataObject.SerializationContext?.Time ?? 0;
						}} else {{
							iNetworkDataObject.Collection.MarkForUpdate(iNetworkDataObject.PropertyIndex);
						}}
					}}

					var oldValue = (({NetworkPropertyPrefix})this).{data.Name};
					(({NetworkPropertyPrefix})this).{data.Name} = value;
				");
				
				if (data.ExposeEvent) {
					GenerateEventInvocation(writer, propertyIndex, data);
				}
				
			}
		}
		
	}
}