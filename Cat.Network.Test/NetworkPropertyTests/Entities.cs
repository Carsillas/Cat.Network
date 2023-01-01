using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Test.NetworkPropertyTests;

public class TestRecursiveCompoundPropertyEntity : NetworkEntity {

	public CompoundNetworkProperty<RecursiveCompoundNetworkProperty> RecursiveProperty { get; } = new CompoundNetworkProperty<RecursiveCompoundNetworkProperty>();

}


public class FieldNetworkPropertyEntity1 : NetworkEntity {
	public NetworkProperty<int> Test = new NetworkProperty<int>();
}
public class FieldNetworkPropertyEntity2 : NetworkEntity {
	public CompoundNetworkProperty<FieldCompoundNetworkProperty> Test2 = new CompoundNetworkProperty<FieldCompoundNetworkProperty>();
}
public class FieldNetworkPropertyEntity3 : NetworkEntity {
	public CompoundNetworkProperty<FieldCompoundNetworkProperty> Test3 { get; } = new CompoundNetworkProperty<FieldCompoundNetworkProperty>();
}

