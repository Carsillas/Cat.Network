using NUnit.Framework;

namespace Cat.Network.Test;
public class CatNetworkTest {
	public TestServer Server { get; set; }
	public TestEntityStorage ServerEntityStorage { get; set; }

	public TestClient ClientA { get; set; }
	public TestClient ClientB { get; set; }

	public TestTransport ClientATransport { get; set; }
	public TestTransport ClientBTransport { get; set; }

	public TestProxyManager ProxyManagerA { get; set; }
	public TestProxyManager ProxyManagerB { get; set; }


	[SetUp]
	public void Setup() {
		ServerEntityStorage = new TestEntityStorage();
		Server = new TestServer(ServerEntityStorage);

		(ClientA, ClientATransport, ProxyManagerA) = AddClient();
		(ClientB, ClientBTransport, ProxyManagerB) = AddClient();


		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();
	}

	protected (TestClient, TestTransport, TestProxyManager) AddClient() {
		TestProxyManager proxyManager = new TestProxyManager();
		TestClient client = new TestClient(proxyManager);
		TestTransport clientTransport = new TestTransport();
		TestTransport serverTransport = new TestTransport();

		clientTransport.Remote = serverTransport;
		serverTransport.Remote = clientTransport;
		Server.AddTransport(clientTransport, new TestProfileEntity());
		client.Connect(serverTransport);

		return new(client, clientTransport, proxyManager);
	}

}
