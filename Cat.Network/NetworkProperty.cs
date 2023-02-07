using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cat.Network {

	[Flags]
	public enum NetworkPropertySerializeTrigger {
		Creation = 1 << 0,
		Modification = 1 << 1
	}

	public abstract class NetworkProperty {
		private NetworkEntity _Entity;
		internal NetworkEntity Entity {
			get => _Entity;
			set {
				_Entity = value;
			}
		}

		public NetworkPropertySerializeTrigger Triggers { get; }

		internal bool CreateDirty { get; set; }
		internal bool Dirty { get; set; }

		internal byte[] PropertyNameBytes { get; set; }

		protected NetworkProperty(NetworkPropertySerializeTrigger triggers) {
			Triggers = triggers;
		}

		public void MarkDirty() {
			Dirty = true;
			Entity.Serializer.UpdateDirty = true;
			Entity.Serializer.CreateDirty = true;
		}

		internal abstract void Serialize(BinaryWriter writer);
		internal abstract void Deserialize(BinaryReader reader);

		internal abstract void Initialize(SerializationContext context);
	}

	public sealed class NetworkProperty<T> : NetworkProperty {
		public delegate void NetworkPropertyChangedDelegate(T Previous);

		public event NetworkPropertyChangedDelegate OnValueChanged;
		private Action<BinaryWriter, T> SerializeFunction { get; set; } = null;
		private Func<BinaryReader, NetworkProperty<T>, T> DeserializeFunction { get; set; } = null;


		public NetworkProperty(NetworkPropertySerializeTrigger triggers = NetworkPropertySerializeTrigger.Creation | NetworkPropertySerializeTrigger.Modification) : base(triggers) {
			_ResolutionFunction = DefaultResolutionFunction;
		}

		private T _Value;
		public T Value {
			get {
				return _ResolutionFunction.Invoke();
			}
			set {
				UpdateValue(value, true);
			}
		}


		private Func<T> _ResolutionFunction;
		internal Func<T> ResolutionFunction {
			private get => _ResolutionFunction;
			set {
				_ResolutionFunction = value ?? DefaultResolutionFunction;
			}
		}

		private T DefaultResolutionFunction() {
			return _Value;
		}

		internal override void Serialize(BinaryWriter writer) {
			SerializeFunction.Invoke(writer, Value);
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
					MarkDirty();
				}
			}
		}

		internal override void Initialize(SerializationContext context) {
			SerializeFunction = context.GetSerializationFunction<T>();
			DeserializeFunction = context.GetDeserializationFunction<T>();
		}
	}
}
