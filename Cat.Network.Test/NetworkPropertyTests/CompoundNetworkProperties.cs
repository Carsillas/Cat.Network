using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cat.Network.Properties;

namespace Cat.Network.Test.NetworkPropertyTests;

public class VectorCompoundNetworkProperty {

	public NetworkProperty<float> X { get; } = new NetworkProperty<float>();
	public NetworkProperty<float> Y { get; } = new NetworkProperty<float>();

}

public class RecursiveCompoundNetworkProperty {

	public NetworkProperty<string> Name { get; } = new NetworkProperty<string>();
	public CompoundNetworkProperty<VectorCompoundNetworkProperty> Vector { get; } = new CompoundNetworkProperty<VectorCompoundNetworkProperty>();

}

public class FieldCompoundNetworkProperty {

	public NetworkProperty<string> Name = new NetworkProperty<string>();
	public CompoundNetworkProperty<VectorCompoundNetworkProperty> Vector = new CompoundNetworkProperty<VectorCompoundNetworkProperty>();

}
