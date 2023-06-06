using System.Collections.Immutable;

namespace Cat.Network.Generator {
	public struct NetworkPropertyData {
		public bool Declared { get; set; }
		public string Name { get; set; }
		public TypeInfo TypeInfo { get; set; }
		public string InterfacePropertyDeclaration => $"{TypeInfo.FullyQualifiedTypeName} {Name} {{ get; set; }}";

	}

	public struct TypeInfo {
		
		public bool IsNullable { get; set; }
		public string FullyQualifiedTypeName { get; set; }
		public ImmutableArray<string> GenericArgumentFQNs { get; set; }

	}

}
