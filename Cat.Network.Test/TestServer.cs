using NUnit.Framework;
using System.Collections.Generic;

namespace Cat.Network.Test;
public class TestServer : CatServer {
	public TestServer(IEntityStorage entityStorage) : base(null, entityStorage) {	}

	public new void Despawn(NetworkEntity entity) {
		base.Despawn(entity);
	}

}
