using NUnit.Framework;

namespace Cat.Network.Test;
public class CatNetworkTest {

	protected MemoryTracker? MemoryTracker { get; set; }

	protected TestServer Server { get; set; }
	protected TestEntityStorage ServerEntityStorage { get; set; }

	protected TestClient ClientA { get; set; }
	protected TestClient ClientB { get; set; }

	protected TestTransport ClientATransport { get; set; }
	protected TestTransport ClientBTransport { get; set; }

	protected TestProxyManager ProxyManagerA { get; set; }
	protected TestProxyManager ProxyManagerB { get; set; }


	[OneTimeSetUp]
	public void OneTimeSetUp() {
		using MemoryTracker tracker = new MemoryTracker(true);
	}

	[TearDown]
	public void TearDown() {
		MemoryTracker?.Dispose();
	}

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
