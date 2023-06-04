using System;
using System.Collections.Generic;
using System.Text;
using static Cat.Network.Generator.Utils;

namespace Cat.Network.Generator {
	public struct NetworkCollectionData {
		public bool Declared { get; set; }
		public string Name { get; set; }
		public string FullyQualifiedTypeName { get; set; }
		public string Item1FullyQualifiedTypeName { get; set; }
		public string Item2FullyQualifiedTypeName { get; set; }

		public string BackingCollectionName => $"__{Name}";
		public string InterfaceCollectionDeclaration => $"{FullyQualifiedTypeName} {Name} {{ get; }}";
		public string ExposedInterfaceCollectionDeclaration => $"{NetworkListFQN}<{Item1FullyQualifiedTypeName}> {BackingCollectionName} {{ get; set; }}";
		public string ExposedExplicitInterfaceCollectionImplementation => $"{NetworkListFQN}<{Item1FullyQualifiedTypeName}> {NetworkCollectionPrefix}.{BackingCollectionName} {{ get; set; }}";
		public string ExposedInterfaceCollectionImplementation => $"public {NetworkListFQN}<{Item1FullyQualifiedTypeName}> {Name} => (({NetworkCollectionPrefix})this).{BackingCollectionName};";

	}
}
