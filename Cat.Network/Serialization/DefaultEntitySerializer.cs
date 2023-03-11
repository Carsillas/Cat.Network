using Cat.Network.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Serialization;
public class DefaultEntitySerializer : IEntitySerializer {

	public DefaultEntitySerializer(SerializationOptions options) {
	
	}

	public void ReadEntityContent(NetworkEntity entity, ReadOnlySpan<byte> buffer) {
		throw new NotImplementedException();
	}

	public void WriteEntityContent(Span<byte> buffer) {
		throw new NotImplementedException();
	}
}
