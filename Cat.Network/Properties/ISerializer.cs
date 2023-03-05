using Cat.Network.Entities;
using Cat.Network.Properties;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Cat.Network.Properties {
	public class Serializer {

		// void Serialize<T>(ref T value) where T : unmanaged;
		// void Deserialize<T>(ref T value) where T : unmanaged;

		public void Serialize(Span<byte> bytes, int value) {
			BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
		}

		void Deserialize(ReadOnlySpan<byte> bytes, out int value) {
			value = BinaryPrimitives.ReadInt32LittleEndian(bytes);
		}

	}
}

