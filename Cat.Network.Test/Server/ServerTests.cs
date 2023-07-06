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

		Assert.IsTrue(ServerEntityStorage.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityServer));
		Assert.AreEqual(testEntityA.GetType(), entityServer.GetType());

		Assert.IsTrue(ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB));
		Assert.AreEqual(testEntityA.GetType(), entityB.GetType());

		TestEntity testEntityServer = (TestEntity)entityServer;
		TestEntity testEntityB = (TestEntity)entityB;

		Assert.AreEqual(testEntityA.Health, testEntityServer.Health);
		Assert.AreEqual(testEntityA.Health, testEntityB.Health);

		ClientA.Despawn(testEntityA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		Assert.IsFalse(ServerEntityStorage.TryGetEntityByNetworkID(testEntityA.NetworkID, out _));
		Assert.IsFalse(ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out _));

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

		ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB);

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

		Assert.IsFalse(ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB));
	}


	[Test]
	public void Test_ServerDespawn() {

		TestEntity testEntityA = new TestEntity();
		ClientA.Spawn(testEntityA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		Assert.IsTrue(ServerEntityStorage.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityServer));

		Assert.IsTrue(ClientA.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityA));
		Assert.IsTrue(ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB));
		Server.Despawn(entityServer);

		Server.Tick();
		ClientA.Tick();
		ClientB.Tick();

		Assert.IsFalse(ClientA.TryGetEntityByNetworkID(testEntityA.NetworkID, out entityA));
		Assert.IsFalse(ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out entityB));

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

		ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB);
		TestEntity testEntityB = (TestEntity)entityB;


		Assert.AreEqual(123, testEntityB.Health);

		testEntityA.Health = 100;

		ClientA.Tick();

		var (ClientC, ClientCTransport, ProxyManagerC) = AddClient();

		Server.Tick();
		ClientB.Tick();
		ClientC.Tick();

		ClientC.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityC);
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
	public void Test_EntityRPC() {
		TestEntity testEntityA = new TestEntity {
			Health = 123
		};

		ClientA.Spawn(testEntityA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		Assert.IsTrue(ServerEntityStorage.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityServer));
		Assert.AreEqual(testEntityA.GetType(), entityServer.GetType());

		Assert.IsTrue(ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB));
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


}