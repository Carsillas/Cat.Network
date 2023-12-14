using System.Collections.Immutable;
using System.Linq;

namespace Cat.Network.Generator {
	public struct RpcMethodData {

		public bool Declared { get; set; }

		public string Name { get; set; }
		public string InterfaceMethodDeclaration { get; set; }
		public string ClassMethodDeclaration { get; set; }
		public string InterfaceMethodInvocationParameters { get; set; }
		
		public bool IsAutoEvent { get; set; }

		public ImmutableArray<RpcParameterData> InterfaceParameters { get; set; }
		public ImmutableArray<RpcParameterData> ClassParameters { get; set; }

		public string GetDelegateDefinitionParameters(NetworkEntityClassDefinition classDefinition) =>
			$"{classDefinition.Name} sender{string.Concat(InterfaceParameters.Select(parameter => $", {parameter.TypeInfo.FullyQualifiedTypeName} {parameter.Name}"))}";

		public string DelegateInvocationParameters =>
			string.Join(", ", new object[] { "this", InterfaceMethodInvocationParameters });

	}

	public enum RpcParameterAttribute {
		Client,
		Instigator
	}

	public struct RpcParameterData {
		public string Name { get; set; }
		public TypeInfo TypeInfo { get; set; }

		public RpcParameterAttribute? SpecialAttribute { get; set; }
		
		public string SerializationExpression { get; set; }
		public string DeserializationExpression { get; set; }
	}

}
