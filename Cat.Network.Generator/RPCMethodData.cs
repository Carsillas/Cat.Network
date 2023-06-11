using System.Collections.Immutable;

namespace Cat.Network.Generator {
	public struct RPCMethodData {

		public bool Declared { get; set; }
		public string InterfaceMethodDeclaration { get; set; }
		public string ClassMethodInvocation { get; set; }

		public ImmutableArray<RPCParameterData> Parameters { get; set; }

	}

	public struct RPCParameterData {
		public string ParameterName { get; set; }
		public TypeInfo TypeInfo { get; set; }

		public string SerializationExpression { get; set; }
		public string DeserializationExpression { get; set; }
	}

}
