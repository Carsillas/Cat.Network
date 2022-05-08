using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cat.Network {
	public abstract class NetworkProperty {
		private NetworkEntity _Entity;
		internal NetworkEntity Entity {
			get => _Entity;
			set {
				_Entity = value;
			}
		}
		internal bool Dirty { get; set; }

		internal abstract void Serialize(BinaryWriter writer);
		internal abstract void Deserialize(BinaryReader reader);

		internal abstract void Initialize(SerializationContext context);
	}

	public sealed class NetworkProperty<T> : NetworkProperty {
		public delegate void NetworkPropertyChangedDelegate(T Previous);

		public event NetworkPropertyChangedDelegate OnValueChanged;
		private Action<BinaryWriter, T> SerializeFunction { get; set; } = null;
		private Func<BinaryReader, NetworkProperty<T>, T> DeserializeFunction { get; set; } = null;

		public NetworkProperty() {
			ResolutionFunction = DefaultResolutionFunction;
		}

		private T _Value;
		public T Value {
			get {
				return ResolutionFunction.Invoke();
			}
			set {
				UpdateValue(value, true);
			}
		}

		private Func<T> ResolutionFunction { get; set; }

		private T DefaultResolutionFunction() {
			return _Value;
		}

		internal override void Serialize(BinaryWriter writer) {
			SerializeFunction.Invoke(writer, _Value);
		}
		internal override void Deserialize(BinaryReader reader) {
			T newValue = DeserializeFunction.Invoke(reader, this);
			UpdateValue(newValue, Entity.Serializer.SerializationContext.DeserializeDirtiesProperty);
		}

		private void UpdateValue(T value, bool markDirty) {
			T previous = Value;
			_Value = value;

			if (!EqualityComparer<T>.Default.Equals(previous, value)) {
				OnValueChanged?.Invoke(previous);
				if (markDirty) {
					Dirty = true;
					Entity.Serializer.Dirty = true;
				}
			}
		}

		internal override void Initialize(SerializationContext context) {
			SerializeFunction = context.GetSerializationFunction<T>();
			DeserializeFunction = context.GetDeserializationFunction<T>();
		}
	}
}
