using static Cat.Network.Generator.Utils;

// @formatter:csharp_max_line_length 400

namespace Cat.Network.Generator {
	public class NetworkEntityPropertyGenerator : NetworkSerializablePropertyGenerator {
		
		
		protected override string SerializableTypeKind { get; } = "class";
		protected override string BaseFQN { get; } = NetworkEntityFQN;
		protected override string InterfaceFQN { get; } = NetworkEntityInterfaceFQN;
		
		protected override void GenerateAdditionalPropertyDefinition(ScopedStringWriter writer, NetworkSerializableClassDefinition classDefinition, NetworkPropertyData data) {
			if (data.ExposeEvent) {
				writer.AppendLine($"public event {NetworkPropertyChangedEventFQN}<{classDefinition.Name}, {data.TypeInfo.FullyQualifiedTypeName}> {data.Name}Changed;");
			}
		}

		private void GenerateEventInvocation(ScopedStringWriter writer, int propertyIndex, NetworkPropertyData data) {
			writer.AppendBlock($@"
				if (changed) {{
					{NetworkPropertyChangedEventArgsFQN}<{data.TypeInfo.FullyQualifiedTypeName}> args = new {NetworkPropertyChangedEventArgsFQN}<{data.TypeInfo.FullyQualifiedTypeName}> {{
						Index = {propertyIndex},
						Name = nameof({data.Name}),
						PreviousValue = oldValue,
						CurrentValue = (({NetworkPropertyPrefix})this).{data.Name}
					}};

					{data.Name}Changed?.Invoke(this, args);
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
					{NetworkEntityInterfaceFQN} iEntity = this;
					ref {NetworkPropertyInfoFQN} networkPropertyInfo = ref iEntity.NetworkProperties[{propertyIndex}];

					if (iEntity.SerializationContext?.IsDeserializing == false || iEntity.SerializationContext?.DeserializeDirtiesProperty == true) {{
						iEntity.LastDirtyTick = iEntity.SerializationContext.Time;
						networkPropertyInfo.LastSetTick = iEntity.SerializationContext.Time;
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
						
							if (iEntity.SerializationContext?.IsDeserializing == false || iEntity.SerializationContext?.DeserializeDirtiesProperty == true) {{
								for (int networkPropertyIndex = 0; networkPropertyIndex < newValue.NetworkProperties.Length; networkPropertyIndex++) {{
									ref {NetworkPropertyInfoFQN} prop = ref newValue.NetworkProperties[networkPropertyIndex];
									prop.LastSetTick = iEntity.SerializationContext.Time;
								}}
							}}
						}}
					");
				}

				if (data.TypeInfo.IsNetworkDataObject) {
					writer.AppendBlock($@"
						bool changed = !ReferenceEquals(oldValue, (({NetworkPropertyPrefix})this).{data.Name});
					");
				} else {
					writer.AppendBlock($@"
						bool changed = !System.Collections.Generic.EqualityComparer<{data.TypeInfo.FullyQualifiedTypeName}>.Default.Equals(oldValue, (({NetworkPropertyPrefix})this).{data.Name});
					");
				}
				writer.AppendBlock($@"
					if (changed) {{
						{NetworkPropertyChangedEventArgsFQN} args = new {NetworkPropertyChangedEventArgsFQN} {{
							Index = {propertyIndex},
							Name = nameof({data.Name})
						}};

						iEntity.OnPropertyChanged(args);
					}}
				");

				if (data.ExposeEvent) {
					GenerateEventInvocation(writer, propertyIndex, data);
				}
			}
		}
		
	}
}