using Cat.Network.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Serialization;
internal interface IEntitySerializer {

	int WriteEntityContent(ISerializationContext context, Span<byte> buffer, NetworkEntity entity);

	void ReadEntityContent(ISerializationContext context, ReadOnlySpan<byte> buffer, ref NetworkEntity entity);

}
