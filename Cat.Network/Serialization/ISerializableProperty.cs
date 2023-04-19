using Cat.Network.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Serialization;


public struct SerializationData {

	public ISerializationContext Context { get; internal init; }
	public MemberSerializationMode Mode { get; internal init; }

}

public interface ISerializableProperty {
	void Read(SerializationData serializationData, ReadOnlySpan<byte> buffer);
	int Write(SerializationData serializationData, Span<byte> buffer);

	void Clean();
}
