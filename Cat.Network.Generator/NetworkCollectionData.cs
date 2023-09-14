using System;
using System.Collections.Generic;
using System.Text;
using static Cat.Network.Generator.Utils;

namespace Cat.Network.Generator {
	public struct NetworkCollectionData {
		public bool Declared { get; set; }
		public string Name { get; set; }


		public TypeInfo CollectionTypeInfo { get; set; }
		public TypeInfo ItemTypeInfo { get; set; }


		public string BackingCollectionName => $"__{Name}";
		public string InterfaceCollectionDeclaration => $"{CollectionTypeInfo.FullyQualifiedTypeName} {Name} {{ get; }}";
		public string ExposedInterfaceCollectionDeclaration => $"{NetworkValueListFQN}<{ItemTypeInfo.FullyQualifiedTypeName}> {BackingCollectionName} {{ get; set; }}";
		public string ExposedExplicitInterfaceCollectionImplementation => $"{NetworkValueListFQN}<{ItemTypeInfo.FullyQualifiedTypeName}> {NetworkCollectionPrefix}.{BackingCollectionName} {{ get; set; }}";
		public string ExposedInterfaceCollectionImplementation => $"public {NetworkValueListFQN}<{ItemTypeInfo.FullyQualifiedTypeName}> {Name} => (({NetworkCollectionPrefix})this).{BackingCollectionName};";


		public string ItemSerializationExpression { get; set; }
		public string ItemDeserializationExpression { get; set; }

	}
}
