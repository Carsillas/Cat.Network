using Cat.Network.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Serialization;
public interface IPacketSerializer {

	void ReadUpdateEntity(NetworkEntity targetEntity, ReadOnlySpan<byte> content);

	NetworkEntity ReadCreateEntity(Guid networkID, ReadOnlySpan<byte> content);
	int WriteCreateEntity(NetworkEntity targetEntity, Span<byte> content);
	int WriteUpdateEntity(NetworkEntity targetEntity, Span<byte> content);
}
