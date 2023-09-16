﻿using System;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using static Cat.Network.Generator.Utils;

namespace Cat.Network.Generator {
	public class NetworkSerializablePropertyGenerator {
		
		public string GenerateNetworkPropertySource(NetworkSerializableClassDefinition classDefinition) {

			return $@"

namespace {classDefinition.Namespace} {{

	partial class {classDefinition.Name} : {NetworkEntityInterfaceFQN}, {classDefinition.Name}.{NetworkPropertyPrefix} {{

{GenerateNetworkPropertyInterface(classDefinition)}
{GenerateNetworkPropertyDefinitions(classDefinition)}

	}}
}}
";
		}


		private string GenerateNetworkPropertyInterface(NetworkSerializableClassDefinition classDefinition) {
			bool isNetworkEntity = $"{classDefinition.Namespace}.{classDefinition.Name}" == NetworkEntityFQN;
			string superInterface = isNetworkEntity ? "" : $": {classDefinition.BaseTypeFQN}.{NetworkPropertyPrefix} ";
			return $@"
		protected new interface {NetworkPropertyPrefix} {superInterface}{{
{GenerateInterfaceProperties(classDefinition)}
		}}
";
		}

		private string GenerateInterfaceProperties(NetworkSerializableClassDefinition classDefinition) {

			StringBuilder stringBuilder = new StringBuilder();

			foreach (NetworkPropertyData property in classDefinition.NetworkProperties.Where(property => property.Declared)) {
				stringBuilder.AppendLine($"\t\t\t{property.InterfacePropertyDeclaration}");
			}

			return stringBuilder.ToString();

		}

		private string GenerateNetworkPropertyDefinitions(NetworkSerializableClassDefinition classDefinition) {

			StringBuilder stringBuilder = new StringBuilder();

			int declaredPropertiesStartIndex = classDefinition.NetworkProperties.Length - classDefinition.NetworkProperties.Count(property => property.Declared);

			int i = 0;
			foreach (NetworkPropertyData data in classDefinition.NetworkProperties.Where(property => property.Declared)) {
				
				if(data.ExposeEvent){
					stringBuilder.AppendLine($"\t\tpublic event {NetworkPropertyChangedEventFQN}<{classDefinition.Name}, {data.TypeInfo.FullyQualifiedTypeName}> {data.Name}Changed;");
				}
				
				stringBuilder.AppendLine($"\t\tpublic {data.TypeInfo.FullyQualifiedTypeName} {data.Name} {GenerateGetterSetter(declaredPropertiesStartIndex + i, data)}");
				i++;
			}

			string GenerateGetterSetter(int propertyIndex, NetworkPropertyData data) {

				string eventInvocation = $@"
				{NetworkPropertyChangedEventArgsFQN}<{data.TypeInfo.FullyQualifiedTypeName}> args = new {NetworkPropertyChangedEventArgsFQN}<{data.TypeInfo.FullyQualifiedTypeName}> {{
					PreviousValue = oldValue,
					CurrentValue = (({NetworkPropertyPrefix})this).{data.Name}
				}};

				if (!System.Collections.Generic.EqualityComparer<{data.TypeInfo.FullyQualifiedTypeName}>.Default.Equals(args.PreviousValue, args.CurrentValue)) {{
					{data.Name}Changed?.Invoke(this, args);
				}}";
				
				return
		$@" {{ 
			get => (({NetworkPropertyPrefix})this).{data.Name};
			set {{ 
				{NetworkEntityInterfaceFQN} iEntity = this;
				ref {NetworkPropertyInfoFQN} networkPropertyInfo = ref iEntity.NetworkProperties[{propertyIndex}];
				iEntity.LastDirtyTick = iEntity.SerializationContext?.Time ?? 0;
				networkPropertyInfo.LastSetTick = iEntity.SerializationContext?.Time ?? 0;
				var oldValue = (({NetworkPropertyPrefix})this).{data.Name};
				(({NetworkPropertyPrefix})this).{data.Name} = value;
				{(data.ExposeEvent ? eventInvocation : "")}
			}}
		}}";

			}

			return stringBuilder.ToString();
		}



	}
}