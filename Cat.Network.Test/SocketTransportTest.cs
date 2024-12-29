using Cat.Network.Test.Server;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Test;

internal class SocketTransportTest {
	protected TestServer Server { get; set; }
	protected TestEntityStorage ServerEntityStorage { get; set; }

	protected TestClient ClientA { get; set; }
	protected TestClient ClientB { get; set; }

	protected SocketTransport ClientATransport { get; set; }
	protected SocketTransport ClientBTransport { get; set; }

	protected TestProxyManager ProxyManagerA { get; set; }
	protected TestProxyManager ProxyManagerB { get; set; }


	private Socket ListenSocket { get; set; }

	private IPEndPoint ListenEndPoint { get; } = new(IPAddress.Loopback, 8192);

	[SetUp]
	public async Task Setup() {
		ListenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		ListenSocket.Bind(ListenEndPoint);
		ListenSocket.Listen();

		ServerEntityStorage = new TestEntityStorage();
		Server = new TestServer(ServerEntityStorage);


		(ClientA, ClientATransport, ProxyManagerA) = await AddClient();
		(ClientB, ClientBTransport, ProxyManagerB) = await AddClient();

		Cycle();
	}

	private void Cycle() {
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

	protected async Task<(TestClient, SocketTransport, TestProxyManager)> AddClient() {
		TestProxyManager proxyManager = new TestProxyManager();
		TestClient client = new TestClient(proxyManager, new TestProfileEntity());

		Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		Socket serverSocket = null;

		await Task.WhenAll(
			clientSocket.ConnectAsync(ListenEndPoint),
			Task.Run(async () => { serverSocket = await ListenSocket.AcceptAsync(); })
		);

		SocketTransport clientTransport = new SocketTransport(null, clientSocket);
		SocketTransport serverTransport = new SocketTransport(null, serverSocket);

		Server.AddTransport(serverTransport, client.ProfileEntity);
		client.Connect(clientTransport);

		return new(client, clientTransport, proxyManager);
	}

	[Test]
	public void Test_EntitySpawning() {
		TestEntity testEntityA = new TestEntity {
			Health = 123
		};

		ClientA.Spawn(testEntityA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		Assert.IsTrue(ServerEntityStorage.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityServer));
		Assert.AreEqual(testEntityA.GetType(), entityServer.GetType());

		Assert.IsTrue(ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityB));
		Assert.AreEqual(testEntityA.GetType(), entityB.GetType());

		TestEntity testEntityServer = (TestEntity)entityServer;
		TestEntity testEntityB = (TestEntity)entityB;

		Assert.AreEqual(testEntityA.Health, testEntityServer.Health);
		Assert.AreEqual(testEntityA.Health, testEntityB.Health);

		ClientA.Despawn(testEntityA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		Assert.IsFalse(ServerEntityStorage.TryGetEntityByNetworkId(testEntityA.NetworkId, out _));
		Assert.IsFalse(ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out _));
	}


	[Test]
	public void Test_SocketClosed() {
		TestEntity testEntityA = new TestEntity {
			Health = 123
		};
		TestEntity testEntityA2 = new TestEntity {
			Health = 123
		};
		
		TestEntity testEntityB = new TestEntity {
			Health = 123
		};

		ClientA.Spawn(testEntityA);
		ClientB.Spawn(testEntityB);
		
		Cycle();
		
		ClientA.Spawn(testEntityA2);
		ClientBTransport.Dispose();
		
		ClientA.Tick();
		Server.Tick();
		ClientA.Tick();
		Server.Tick();
		ClientA.Tick();
		
		Assert.IsTrue(ClientA.TryGetEntityByNetworkId(testEntityB.NetworkId, out NetworkEntity entityB));
		Assert.IsTrue(entityB.IsOwner);
		
		//ClientB.Tick();

	}
}