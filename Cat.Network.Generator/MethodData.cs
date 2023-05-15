using System.Collections.Immutable;

namespace Cat.Network.Generator {
	public struct RPCMethodData {
		public string InterfaceMethodDeclaration { get; set; }
		public string ClassMethodInvocation { get; set; }

		public ImmutableArray<RPCParameterData> Parameters { get; set; }

	}

	public struct RPCParameterData {
		public string ParameterName { get; set; }
		public string FullyQualifiedTypeName { get; set; }
	}

}
