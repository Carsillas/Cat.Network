using static Cat.Network.Generator.Utils;

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
					{NetworkEntityInterfaceFQN} iEntity = this;
					ref {NetworkPropertyInfoFQN} networkPropertyInfo = ref iEntity.NetworkProperties[{propertyIndex}];
					iEntity.LastDirtyTick = iEntity.SerializationContext?.Time ?? 0;
					networkPropertyInfo.LastSetTick = iEntity.SerializationContext?.Time ?? 0;
					var oldValue = (({NetworkPropertyPrefix})this).{data.Name};
					(({NetworkPropertyPrefix})this).{data.Name} = value;
				");

				if (data.TypeInfo.IsNetworkDataObject) {
					writer.AppendBlock($@"
						if (oldValue != null) {{
							(({NetworkDataObjectInterfaceFQN})oldValue).Parent = null;
							(({NetworkDataObjectInterfaceFQN})oldValue).PropertyIndex = -1;
						}}
						{NetworkDataObjectInterfaceFQN} newValue = value;
						if (newValue != null) {{
							newValue.Parent = this;
							newValue.PropertyIndex = {propertyIndex};
						
							for (int networkPropertyIndex = 0; networkPropertyIndex < newValue.NetworkProperties.Length; networkPropertyIndex++) {{
								ref {NetworkPropertyInfoFQN} prop = ref newValue.NetworkProperties[networkPropertyIndex];
								prop.LastSetTick = iEntity.SerializationContext?.Time ?? 0;
							}}
						}}
					");
				}

				if (data.ExposeEvent) {
					GenerateEventInvocation(writer, propertyIndex, data);
				}
			}
		}
		
	}
}