using Cat.Network.Entities;
using NUnit.Framework;
using System;

namespace Cat.Network.Test {
	public class ServerTest : CatNetworkTest {

		[Test]
		public void Test_EntitySpawning() {
			TestEntity testEntityA = new TestEntity {
				TestInt = 123
			};

			ClientA.Spawn(testEntityA);

			ClientA.Tick();
			Server.Tick();
			ClientB.Tick();

			bool serverHasEntity = ServerEntityStorage.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityServer);
			Assert.IsTrue(serverHasEntity);
			Assert.AreEqual(testEntityA.GetType(), entityServer.GetType());

			Assert.IsTrue(ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB));
			Assert.AreEqual(testEntityA.GetType(), entityB.GetType());

			TestEntity testEntityServer = (TestEntity)entityServer;
			TestEntity testEntityB = (TestEntity)entityB;

			Assert.AreEqual(testEntityA.TestInt, testEntityServer.TestInt);
			Assert.AreEqual(testEntityA.TestInt, testEntityB.TestInt);

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
		public void Test_SimultaneousCreationUpdateRequests() {

			TestEntity testEntityA = new TestEntity {
				TestInt = 123
			};

			ClientA.Spawn(testEntityA);

			ClientA.Tick();
			Server.Tick();
			ClientB.Tick();

			ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB);
			TestEntity testEntityB = (TestEntity)entityB;


			Assert.AreEqual(123, testEntityB.TestInt);

			testEntityA.TestInt = 100;

			ClientA.Tick();

			var (ClientC, ClientCTransport, ProxyManagerC) = AddClient();

			Server.Tick();
			ClientB.Tick();
			ClientC.Tick();

			ClientC.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityC);
			TestEntity testEntityC = (TestEntity)entityC;

			Assert.AreEqual(100, testEntityB.TestInt);
			Assert.AreEqual(100, testEntityC.TestInt);

		}

		[Test]
		public void Test_EntityDirtyReset() {

			TestEntity testEntityA = new TestEntity {
				TestInt = 123
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

			testEntityA.TestInt = 124;

			ClientA.Tick();
			Assert.AreEqual(1, ClientATransport.Messages.Count);
			Assert.AreEqual(1, ClientBTransport.Remote.Messages.Count);

			Server.Tick();
			Assert.AreEqual(0, ClientATransport.Messages.Count);
			Assert.AreEqual(2, ClientBTransport.Remote.Messages.Count);

		}


	}
}