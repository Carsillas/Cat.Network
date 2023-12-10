using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Test;
public class TestClient : CatClient {
	
	public TestProfileEntity ProfileEntity { get; }

	public TestClient(IProxyManager proxyManager, TestProfileEntity profileEntity) : base(null, proxyManager) {
		ProfileEntity = profileEntity;
	}

}
