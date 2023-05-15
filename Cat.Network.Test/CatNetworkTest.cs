using NUnit.Framework;

namespace Cat.Network.Test;
public class CatNetworkTest {

	protected TestServer Server { get; set; }
	protected TestEntityStorage ServerEntityStorage { get; set; }

	protected CatClient ClientA { get; set; }
	protected CatClient ClientB { get; set; }

	protected TestTransport ClientATransport { get; set; }
	protected TestTransport ClientBTransport { get; set; }

	protected TestProxyManager ProxyManagerA { get; set; }
	protected TestProxyManager ProxyManagerB { get; set; }


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

	protected (CatClient, TestTransport, TestProxyManager) AddClient() {
		TestProxyManager proxyManager = new TestProxyManager();
		CatClient client = new CatClient(proxyManager);
		TestTransport clientTransport = new TestTransport();
		TestTransport serverTransport = new TestTransport();

		clientTransport.Remote = serverTransport;
		serverTransport.Remote = clientTransport;
		Server.AddTransport(clientTransport, new TestProfileEntity());
		client.Connect(serverTransport);

		return new(client, clientTransport, proxyManager);
	}

}
