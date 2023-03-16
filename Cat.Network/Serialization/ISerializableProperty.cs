using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Serialization;
public interface ISerializableProperty {
	void Read(ISerializationContext context, MemberSerializationMode mode, ReadOnlySpan<byte> buffer);
	int Write(ISerializationContext context, MemberSerializationMode mode, Span<byte> buffer);
}
