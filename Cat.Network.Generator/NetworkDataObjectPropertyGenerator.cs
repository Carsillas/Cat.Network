
using static Cat.Network.Generator.Utils;

// @formatter:csharp_max_line_length 400

namespace Cat.Network.Generator {
	public class NetworkDataObjectPropertyGenerator : NetworkSerializablePropertyGenerator {
		protected override string SerializableTypeKind { get; } = "record";
		
		protected override string BaseFQN { get; } = NetworkDataObjectFQN;
		protected override string InterfaceFQN { get; } = NetworkDataObjectInterfaceFQN;
		

		private void GenerateEventInvocation(ScopedStringWriter writer, int propertyIndex, NetworkPropertyData data) {

			string changedDefinition = data.TypeInfo.IsNetworkDataObject ?
				$"bool changed = !ReferenceEquals(oldValue, (({NetworkPropertyPrefix})this).{data.Name});" :
				$"bool changed = !System.Collections.Generic.EqualityComparer<{data.TypeInfo.FullyQualifiedTypeName}>.Default.Equals(oldValue, (({NetworkPropertyPrefix})this).{data.Name});";
			
			writer.AppendBlock($@"
				{NetworkPropertyChangedEventArgsFQN} args = new {NetworkPropertyChangedEventArgsFQN} {{
					Index = {propertyIndex},
					Name = nameof({data.Name})
				}};

				{changedDefinition}
				if (changed) {{
					iNetworkDataObject.OnPropertyChanged(args);
				}}
			");
		}
		
		protected override void GenerateGetter(ScopedStringWriter writer, int propertyIndex, NetworkPropertyData data) {
			writer.AppendLine($"get => (({NetworkPropertyPrefix})this).{data.Name};");
		}

		protected override void GenerateSetter(ScopedStringWriter writer, int propertyIndex, NetworkPropertyData data) {
			using (writer.EnterScope("set")) {
				
				if (data.TypeInfo.IsNetworkDataObject) {
					writer.AppendBlock($@"
						{NetworkDataObjectInterfaceFQN} newValue = value;
						if (newValue?.Parent != null) {{
							throw new System.InvalidOperationException($""nameof({NetworkDataObjectFQN})s may only occupy one networked property or list!"");
						}}
					");
				}
				
				writer.AppendBlock($@"
					{NetworkDataObjectInterfaceFQN} iNetworkDataObject = this; // {data.TypeInfo.IsNetworkDataObject}
					
					if (iNetworkDataObject.Anchor != null) {{
						iNetworkDataObject.Anchor.LastDirtyTick = iNetworkDataObject.SerializationContext?.Time ?? 0;
					}}

					ref {NetworkPropertyInfoFQN} networkPropertyInfo = ref iNetworkDataObject.NetworkProperties[{propertyIndex}];
					networkPropertyInfo.LastSetTick = iNetworkDataObject.SerializationContext?.Time ?? 0;
					
					{NetworkDataObjectInterfaceFQN} current = iNetworkDataObject;
					{NetworkSerializableInterfaceFQN} parent = iNetworkDataObject.Parent;
					while (parent != null) {{
						if (current.Collection == null) {{
							ref {NetworkPropertyInfoFQN} parentNetworkPropertyInfo = ref parent.NetworkProperties[current.PropertyIndex];
							parentNetworkPropertyInfo.LastUpdateTick = current.SerializationContext?.Time ?? 0;
						}} else {{
							current.Collection.MarkForUpdate(current.PropertyIndex);
						}}

						current = parent as {NetworkDataObjectInterfaceFQN};
						parent = current?.Parent;
					}}

					var oldValue = (({NetworkPropertyPrefix})this).{data.Name};
					(({NetworkPropertyPrefix})this).{data.Name} = value;
				");
				
				if (data.TypeInfo.IsNetworkDataObject) {
					writer.AppendBlock($@"
						if (oldValue != null) {{
							(({NetworkDataObjectInterfaceFQN})oldValue).Parent = null;
							(({NetworkDataObjectInterfaceFQN})oldValue).PropertyIndex = -1;
						}}
						if (newValue != null) {{
							newValue.Parent = this;
							newValue.PropertyIndex = {propertyIndex};
						
							if (iNetworkDataObject.Anchor?.SerializationContext?.IsDeserializing == false || iNetworkDataObject.Anchor?.SerializationContext?.DeserializeDirtiesProperty == true) {{
								for (int networkPropertyIndex = 0; networkPropertyIndex < newValue.NetworkProperties.Length; networkPropertyIndex++) {{
									ref {NetworkPropertyInfoFQN} prop = ref newValue.NetworkProperties[networkPropertyIndex];
									prop.LastSetTick = iNetworkDataObject.Anchor.SerializationContext.Time;
								}}
							}}
						}}
					");
				}
				
				GenerateEventInvocation(writer, propertyIndex, data);
				
			}
		}
		
	}
}