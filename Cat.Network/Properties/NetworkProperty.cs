using Cat.Network.Entities;
using Cat.Network.Generator;
using Cat.Network.Serialization;
using System;
using System.Buffers.Binary;
using System.Security.Principal;

namespace Cat.Network.Properties {

	public abstract class NetworkProperty {

		public NetworkEntity Entity { get; init; }
		public int Index { get; init; }
		public string Name { get; init; }
		protected internal bool Dirty { get; private protected set; }

		protected bool MarkDirtyOnDeserialize => ((INetworkEntity)Entity)?.SerializationContext?.DeserializeDirtiesProperty ?? true;
		protected ISerializationContext SerializationContext => ((INetworkEntity)Entity)?.SerializationContext;

		protected NetworkProperty() {

		}

		public abstract void Read(MemberSerializationMode mode, ReadOnlySpan<byte> buffer);
		public abstract int Write(MemberSerializationMode mode, Span<byte> buffer);

		internal abstract void Clean();

	}

	public abstract class NetworkProperty<T> : NetworkProperty {

		private T _Value;

		public T Value {
			get => _Value; 
			set {
				_Value = value;
				MarkDirty();
			}
		
		}

		protected void SetValue(T value, bool markDirty) {
			_Value = value;
			if (markDirty) {
				MarkDirty();
			}
		}

		protected void MarkDirty() {
			Entity.LastDirtyTick = SerializationContext?.Time ?? 0;
			Dirty = true;
		}

		internal override void Clean() {
			Dirty = false;
		}

	}

	public class CustomNetworkProperty<T> : NetworkProperty<T> where T : ISerializableProperty {
		public override void Read(MemberSerializationMode mode, ReadOnlySpan<byte> buffer) {
			Value.Read(SerializationContext, mode, buffer);

			if (MarkDirtyOnDeserialize) {
				MarkDirty();
			}
		}
		public override int Write(MemberSerializationMode mode, Span<byte> buffer) => Value.Write(SerializationContext, mode, buffer);


		internal override void Clean() {
			base.Clean();

			Value.Clean();
		}

	}

	public class Int32NetworkProperty : NetworkProperty<int> {
		public override void Read(MemberSerializationMode mode, ReadOnlySpan<byte> buffer) {
			Value = BinaryPrimitives.ReadInt32LittleEndian(buffer);
		}

		public override int Write(MemberSerializationMode mode, Span<byte> buffer) {
			BinaryPrimitives.WriteInt32LittleEndian(buffer, Value);
			return 4;
		}
	}

	public class BooleanNetworkProperty : NetworkProperty<bool> {
		public override void Read(MemberSerializationMode mode, ReadOnlySpan<byte> buffer) {
			SetValue(buffer[0] != 0, MarkDirtyOnDeserialize);
		}

		public override int Write(MemberSerializationMode mode, Span<byte> buffer) {
			buffer[0] = (byte)(Value ? 1 : 0);
			return 1;
		}
	}


}
