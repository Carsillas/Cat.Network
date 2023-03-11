using Cat.Network.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Serialization;
internal interface IEntitySerializer {

	void WriteEntityContent(Span<byte> buffer);
	void ReadEntityContent(NetworkEntity entity, ReadOnlySpan<byte> buffer);

}
