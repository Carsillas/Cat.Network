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

		public abstract void Read(ISerializationContext context, MemberSerializationMode mode, ReadOnlySpan<byte> buffer, int offset);
		public abstract int Write(ISerializationContext context, MemberSerializationMode mode, Span<byte> buffer, int offset);

	}

	public abstract class NetworkProperty<T> : NetworkProperty {

		public T Value;

	}

	public class CustomNetworkProperty<T> : NetworkProperty<T> where T : ISerializableProperty {
		public override void Read(ISerializationContext context, MemberSerializationMode mode, ReadOnlySpan<byte> buffer, int offset) => Value.Read(context, mode, buffer, offset);

		public override int Write(ISerializationContext context, MemberSerializationMode mode, Span<byte> buffer, int offset) => Value.Write(context, mode, buffer, offset);
	}

	public class Int32NetworkProperty : NetworkProperty<int> {
		public override void Read(ISerializationContext context, MemberSerializationMode mode, ReadOnlySpan<byte> buffer, int offset) {
			Value = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(offset));
		}

		public override int Write(ISerializationContext context, MemberSerializationMode mode, Span<byte> buffer, int offset) {
			BinaryPrimitives.WriteInt32LittleEndian(buffer.Slice(offset), Value);
			return 4;
		}
	}

	public class BooleanNetworkProperty : NetworkProperty<bool> {
		public override void Read(ISerializationContext context, MemberSerializationMode mode, ReadOnlySpan<byte> buffer, int offset) {
			Value = buffer[offset] != 0;
		}

		public override int Write(ISerializationContext context, MemberSerializationMode mode, Span<byte> buffer, int offset) {
			buffer[offset] = (byte)(Value ? 1 : 0);
			return 1;
		}
	}


}
