using Cat.Network.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Serialization;
internal interface IEntitySerializer {

	int WriteEntityContent(ISerializationContext context, NetworkEntity entity, Span<byte> buffer);

	void ReadEntityContent(ISerializationContext context, NetworkEntity entity, ReadOnlySpan<byte> buffer);

}
