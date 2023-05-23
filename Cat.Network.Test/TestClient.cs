using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Test;
public class TestClient : CatClient {

	private TestTransport Transport { get; set; }

	
	public TestClient(IProxyManager proxyManager) : base(proxyManager) { }




	public void Connect(TestTransport transport) {
		Transport = transport;
		base.Connect(transport);
	}

	protected override void PreExecute() {
		base.PreExecute();

		foreach(byte[] packet in Transport.Messages) {
			DeliverPacket(packet);
		}
		Transport.Messages.Clear();

	}

}
