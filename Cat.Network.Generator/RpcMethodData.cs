using System.Collections.Immutable;

namespace Cat.Network.Generator {
	public struct RpcMethodData {

		public bool Declared { get; set; }
		public string InterfaceMethodDeclaration { get; set; }
		public string ClassMethodDeclaration { get; set; }
		public string InterfaceMethodInvocation { get; set; }

		public ImmutableArray<RpcParameterData> InterfaceParameters { get; set; }
		public ImmutableArray<RpcParameterData> ClassParameters { get; set; }

	}

	public enum RpcParameterAttribute {
		Client,
		Instigator
	}

	public struct RpcParameterData {
		public string ParameterName { get; set; }
		public TypeInfo TypeInfo { get; set; }

		public RpcParameterAttribute? SpecialAttribute { get; set; }
		
		public string SerializationExpression { get; set; }
		public string DeserializationExpression { get; set; }
	}

}
