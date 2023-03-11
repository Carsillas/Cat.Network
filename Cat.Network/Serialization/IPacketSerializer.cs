using Cat.Network.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Serialization;
public interface IPacketSerializer {

	void UpdateEntity(NetworkEntity targetEntity, ReadOnlySpan<byte> data);

	NetworkEntity CreateEntity(Guid networkID, ReadOnlySpan<byte> data);

}
