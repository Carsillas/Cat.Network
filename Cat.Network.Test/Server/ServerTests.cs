using NUnit.Framework;
using System;

namespace Cat.Network.Test.Server;

public class ServerTest : CatNetworkTest {


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
	public void Test_EntityOwnershipModification() {

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		TestEntity testEntityA = new TestEntity();

		bool AGainedOwnership = false;
		bool BGainedOwnership = false;

		ProxyManagerA.GainedOwnership += entity => {
			AGainedOwnership = true;
		};

		ProxyManagerB.GainedOwnership += entity => {
			BGainedOwnership = true;
		};

		Assert.IsTrue(testEntityA.IsOwner);

		Assert.IsFalse(AGainedOwnership);
		Assert.IsFalse(BGainedOwnership);

		ClientA.Spawn(testEntityA);
		Assert.IsTrue(testEntityA.IsOwner);

		Assert.IsTrue(AGainedOwnership);
		Assert.IsFalse(BGainedOwnership);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityB);

		Assert.IsTrue(AGainedOwnership);
		Assert.IsFalse(BGainedOwnership);
		Assert.IsFalse(entityB.IsOwner);

		ProxyManagerB.GainedOwnership += entity => {
			Assert.AreSame(entity, entityB);
		};

		Server.RemoveTransport(ClientATransport);

		Assert.IsFalse(BGainedOwnership);
		Assert.IsFalse(entityB.IsOwner);

		Server.Tick();
		ClientB.Tick();

		Assert.IsTrue(BGainedOwnership);
		Assert.IsTrue(entityB.IsOwner);

	}


	[Test]
	public void Test_DestroyWithOwner() {

		TestEntity testEntityA = new TestEntity();
		ClientA.Spawn(testEntityA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		testEntityA.DestroyWithOwner = true;

		ClientA.Tick();
		Server.Tick(); // Server will not read packets from A if it's transport was removed first.

		Server.RemoveTransport(ClientATransport);
		Server.Tick();
		ClientB.Tick();

		Assert.IsFalse(ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityB));
	}
	
	[Test]
	public void Test_NonOwnerModification() {

		TestEntity testEntityA = new TestEntity();
		ClientA.Spawn(testEntityA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityB);
		TestEntity testEntityB = (TestEntity)entityB;

		testEntityB.Health = 123;

		for (int i = 0; i < 3; i++) {
			ClientA.Tick();
			Server.Tick();
			ClientB.Tick();
		}

		Assert.AreEqual(0, testEntityA.Health);
		// TODO? Assert.AreEqual(0, testEntityB.Health);
	}

	[Test]
	public void Test_ServerDespawn() {

		TestEntity testEntityA = new TestEntity();
		ClientA.Spawn(testEntityA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		Assert.IsTrue(ServerEntityStorage.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityServer));

		Assert.IsTrue(ClientA.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityA));
		Assert.IsTrue(ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityB));
		Server.Despawn(entityServer);

		Server.Tick();
		ClientA.Tick();
		ClientB.Tick();

		Assert.IsFalse(ClientA.TryGetEntityByNetworkId(testEntityA.NetworkId, out entityA));
		Assert.IsFalse(ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out entityB));

	}

	[Test]
	public void Test_SimultaneousCreationUpdateRequests() {

		TestEntity testEntityA = new TestEntity {
			Health = 123
		};

		ClientA.Spawn(testEntityA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityB);
		TestEntity testEntityB = (TestEntity)entityB;


		Assert.AreEqual(123, testEntityB.Health);

		testEntityA.Health = 100;

		ClientA.Tick();

		var (ClientC, ClientCTransport, ProxyManagerC) = AddClient();

		Server.Tick();
		ClientB.Tick();
		ClientC.Tick();

		ClientC.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityC);
		TestEntity testEntityC = (TestEntity)entityC;

		Assert.AreEqual(100, testEntityB.Health);
		Assert.AreEqual(100, testEntityC.Health);

	}

	[Test]
	public void Test_EntityDirtyReset() {

		TestEntity testEntityA = new TestEntity {
			Health = 123
		};

		ClientA.Spawn(testEntityA);

		ClientA.Tick();
		Assert.AreEqual(1, ClientATransport.Messages.Count);
		Assert.AreEqual(0, ClientBTransport.Remote.Messages.Count);

		Server.Tick();
		Assert.AreEqual(0, ClientATransport.Messages.Count);
		Assert.AreEqual(1, ClientBTransport.Remote.Messages.Count);

		ClientA.Tick();
		Assert.AreEqual(0, ClientATransport.Messages.Count);
		Assert.AreEqual(1, ClientBTransport.Remote.Messages.Count);

		Server.Tick();
		Assert.AreEqual(0, ClientATransport.Messages.Count);
		Assert.AreEqual(1, ClientBTransport.Remote.Messages.Count);

		testEntityA.Health = 124;

		ClientA.Tick();
		Assert.AreEqual(1, ClientATransport.Messages.Count);
		Assert.AreEqual(1, ClientBTransport.Remote.Messages.Count);


		Server.Tick();
		Assert.AreEqual(0, ClientATransport.Messages.Count);
		Assert.AreEqual(2, ClientBTransport.Remote.Messages.Count);

	}


	[Test]
	public void Test_EntityRpc() {
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

		testEntityB.ModifyHealth(10);

		ClientB.Tick();

		Assert.AreEqual(123, testEntityB.Health);
		Assert.AreEqual(123, testEntityA.Health);

		Server.Tick();
		ClientA.Tick();

		Assert.AreEqual(123, testEntityB.Health);
		Assert.AreEqual(133, testEntityA.Health);

		Server.Tick();
		ClientB.Tick();

		Assert.AreEqual(133, testEntityB.Health);
		Assert.AreEqual(133, testEntityA.Health);

	}

	[Test]
	public void Test_EntityRpcAutoParameters() {
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

		bool callbackExecuted = false;

		testEntityA.OnVerifyAutoParametersRpc += (sender, client, guid) => {
			Assert.AreSame(ClientA, client);
			Assert.AreEqual(guid, ClientB.ProfileEntity.NetworkId);
			
			callbackExecuted = true;
		};

		testEntityB.VerifyAutoParametersRpc();
		
		ClientB.Tick();
		Server.Tick();
		ClientA.Tick();

		Assert.IsTrue(callbackExecuted);

	}


	[Test]
	public void Test_EntityOwnershipForfeit() {
		TestEntity testEntityA = new TestEntity();

		ClientA.Spawn(testEntityA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		ServerEntityStorage.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityServer);
		ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityB);
		
		bool ALostOwnership = false;
		bool BLostOwnership = false;
		bool AGainedOwnership = false;
		bool BGainedOwnership = false;

		ProxyManagerA.ForfeitedOwnership += entity => {
			ALostOwnership = true;
		};

		ProxyManagerB.GainedOwnership += entity => {
			BGainedOwnership = true;
		};
		

		ProxyManagerA.GainedOwnership += entity => {
			AGainedOwnership = true;
		};

		ProxyManagerB.ForfeitedOwnership += entity => {
			BLostOwnership = true;
		};
		
		ClientA.Disown(testEntityA, ClientB.ProfileEntity.NetworkId);

		Assert.IsTrue(ALostOwnership);
		Assert.IsFalse(testEntityA.IsOwner);
		
		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		Assert.IsTrue(BGainedOwnership);
		Assert.IsTrue(entityB.IsOwner);
		
		ClientB.Disown(entityB, ClientA.ProfileEntity.NetworkId);

		Assert.IsTrue(BLostOwnership);
		Assert.IsFalse(entityB.IsOwner);
		
		ClientB.Tick();
		Server.Tick();
		ClientA.Tick();

		Assert.IsTrue(AGainedOwnership);
		Assert.IsTrue(testEntityA.IsOwner);
		
	}

	
	
}