using System;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network {

	public abstract class CompoundNetworkProperty { 
	
		protected internal abstract object InternalValue { get; }

	}

	public class CompoundNetworkProperty<T> : CompoundNetworkProperty where T : class, new() {
		protected internal override object InternalValue => Value;

		public T Value { get; } = new T();

	}
}
