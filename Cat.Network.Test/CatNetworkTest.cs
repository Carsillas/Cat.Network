using Cat.Network.Serialization;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
		Server = new TestServer(ServerEntityStorage, new DefaultPacketSerializer());

		(ClientA, ClientATransport, ProxyManagerA) = AddClient();
		(ClientB, ClientBTransport, ProxyManagerB) = AddClient();
	}

	protected (CatClient, TestTransport, TestProxyManager) AddClient() {
		TestProxyManager proxyManager = new TestProxyManager();
		CatClient client = new CatClient(proxyManager, new DefaultPacketSerializer());
		TestTransport clientTransport = new TestTransport();
		TestTransport serverTransport = new TestTransport();

		clientTransport.Remote = serverTransport;
		serverTransport.Remote = clientTransport;
		Server.AddTransport(clientTransport, new TestProfileEntity());
		client.Connect(serverTransport);

		return new(client, clientTransport, proxyManager);
	}

}
