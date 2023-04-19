using Cat.Network.Serialization;
using Cat.Network.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Test;
public class TestServer : CatServer {
	public TestServer(IEntityStorage entityStorage) : base(entityStorage) {	}

	public new void RemoveTransport(ITransport transport) {
		base.RemoveTransport(transport);
	}

}
