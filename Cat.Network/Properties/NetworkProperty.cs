using System;
using System.Collections.Generic;
using System.Text;
using Cat.Network.Entities;

namespace Cat.Network.Properties {

	public abstract class NetworkProperty {

		protected NetworkProperty() {

		}

	}

	public sealed class NetworkProperty<T> : NetworkProperty {

		public delegate void NetworkPropertyChangedDelegate(T Previous);

		public event NetworkPropertyChangedDelegate ValueChanged;

		private T _Value;
		public T Value {
			get {
				return _Value;
			}
			set {
				UpdateValue(value, true);
			}
		}

		private void UpdateValue(T value, bool markDirty) {
			T previous = Value;
			_Value = value;

		}

	}
}
