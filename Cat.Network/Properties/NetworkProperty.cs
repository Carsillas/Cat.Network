using Cat.Network.Serialization;
using System;
using System.Buffers.Binary;

namespace Cat.Network.Properties {

	public abstract class NetworkProperty : ISerializableProperty {

		public int Index { get; init; }
		public string Name { get; init; }
		public bool Dirty { get; private set; }

		protected NetworkProperty() {

		}

		public abstract void Read(ISerializationContext context, MemberSerializationMode mode, ReadOnlySpan<byte> buffer);
		public abstract int Write(ISerializationContext context, MemberSerializationMode mode, Span<byte> buffer);

	}

	public abstract class NetworkProperty<T> : NetworkProperty {

		public T Value;

	}

	public class CustomNetworkProperty<T> : NetworkProperty<T> where T : ISerializableProperty {
		public override void Read(ISerializationContext context, MemberSerializationMode mode, ReadOnlySpan<byte> buffer) => Value.Read(context, mode, buffer);

		public override int Write(ISerializationContext context, MemberSerializationMode mode, Span<byte> buffer) => Value.Write(context, mode, buffer);
	}

	public class Int32NetworkProperty : NetworkProperty<int> {
		public override void Read(ISerializationContext context, MemberSerializationMode mode, ReadOnlySpan<byte> buffer) {
			Value = BinaryPrimitives.ReadInt32LittleEndian(buffer);
		}

		public override int Write(ISerializationContext context, MemberSerializationMode mode, Span<byte> buffer) {
			BinaryPrimitives.WriteInt32LittleEndian(buffer, Value);
			return 4;
		}
	}

	public class BooleanNetworkProperty : NetworkProperty<bool> {
		public override void Read(ISerializationContext context, MemberSerializationMode mode, ReadOnlySpan<byte> buffer) {
			Value = buffer[0] != 0;
		}

		public override int Write(ISerializationContext context, MemberSerializationMode mode, Span<byte> buffer) {
			buffer[0] = (byte)(Value ? 1 : 0);
			return 1;
		}
	}


}
