using System.Collections.Immutable;

namespace Cat.Network.Generator {
	public struct NetworkPropertyData {
		public bool Declared { get; set; }
		public string Name { get; set; }
		public TypeInfo TypeInfo { get; set; }
		public string InterfacePropertyDeclaration => $"{TypeInfo.FullyQualifiedTypeName} {Name} {{ get; set; }}";

		public bool ExposeEvent { get; set; }
		
		public string CompleteSerializationExpression { get; set; }
		public string CompleteDeserializationExpression { get; set; }
		public string PartialSerializationExpression { get; set; }
		public string PartialDeserializationExpression { get; set; }
	}

	public struct TypeInfo {
		
		public bool IsNullable { get; set; }
		public string FullyQualifiedTypeName { get; set; }
		public ImmutableArray<string> GenericArgumentFQNs { get; set; }
		public bool IsNetworkDataObject { get; set; }

	}

}
