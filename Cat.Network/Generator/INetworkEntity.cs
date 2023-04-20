using Cat.Network.Properties;
using Cat.Network.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network.Generator
{
    public interface INetworkEntity {

		Guid NetworkID { get; }

		void Initialize();

        int Serialize(SerializationOptions serializationOptions, Span<byte> buffer);
        void Deserialize(SerializationOptions serializationOptions, ReadOnlySpan<byte> buffer);

		void Clean();

        NetworkPropertyInfo[] NetworkProperties { get; set; }
		ISerializationContext SerializationContext { get; set; }

		int LastDirtyTick { get; set; }

	}
}
