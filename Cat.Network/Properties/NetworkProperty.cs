using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using Cat.Network.Entities;

namespace Cat.Network.Properties {

	public abstract class NetworkProperty {

		protected NetworkProperty() {

		}

	}

	public class NetworkProperty<T> : NetworkProperty {

		public T Value;

	}

	public class CustomNetworkProperty<T> : NetworkProperty<T> where T : ISerializable {
	
	}

}
