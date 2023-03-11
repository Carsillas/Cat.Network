using Cat.Network.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Server;
internal class RemoteClient {

	public ITransport Transport { get; set; }
	public IEntityProcessor EntityProcessor { get; set; }
	public NetworkEntity ProfileEntity { get; set; }
	
}
