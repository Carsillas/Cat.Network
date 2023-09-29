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


		public string NetworkListTypeFQN =>
			ItemTypeInfo.IsNetworkDataObject ? NetworkObjectListFQN : NetworkValueListFQN;
		
		public string BackingCollectionName => $"__{Name}";
		public string InterfaceCollectionDeclaration => $"{CollectionTypeInfo.FullyQualifiedTypeName} {Name} {{ get; }}";
		public string ExposedInterfaceCollectionDeclaration => $"{NetworkListTypeFQN}<{ItemTypeInfo.FullyQualifiedTypeName}> {BackingCollectionName} {{ get; set; }}";
		public string ExposedExplicitInterfaceCollectionImplementation => $"{NetworkListTypeFQN}<{ItemTypeInfo.FullyQualifiedTypeName}> {NetworkCollectionPrefix}.{BackingCollectionName} {{ get; set; }}";
		public string ExposedInterfaceCollectionImplementation => $"public {NetworkListTypeFQN}<{ItemTypeInfo.FullyQualifiedTypeName}> {Name} => (({NetworkCollectionPrefix})this).{BackingCollectionName};";


		public string CompleteItemSerializationExpression { get; set; }
		public string ItemDeserializationExpression { get; set; }

		public string PartialItemSerializationExpression { get; set; }
		
	}
}
