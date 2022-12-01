using NUnit.Framework;
using System;

namespace Cat.Network.Test
{
    public class ServerTests
    {
        private TestServer Server { get; set; }
        private TestEntityStorage ServerEntityStorage { get; set; }
        
        private Client ClientA { get; set; }
        private Client ClientB { get; set; }

        private TestTransport ClientATransport { get; set; }
		private TestTransport ClientBTransport { get; set; }

        private TestProxyManager ProxyManagerA { get; set; }
        private TestProxyManager ProxyManagerB { get; set; }


		[SetUp]
        public void Setup()
        {
            ServerEntityStorage = new TestEntityStorage();
            Server = new TestServer(ServerEntityStorage);

            ProxyManagerA = new TestProxyManager();
            ProxyManagerB = new TestProxyManager();

            ClientA = new Client(ProxyManagerA);
            ClientB = new Client(ProxyManagerB);

			ClientATransport = new TestTransport();
			ClientBTransport = new TestTransport();
			TestTransport serverATransport = new TestTransport();
			TestTransport serverBTransport = new TestTransport();

			ClientATransport.Remote = serverATransport;
            serverATransport.Remote = ClientATransport;
			ClientBTransport.Remote = serverBTransport;
            serverBTransport.Remote = ClientBTransport;

            Server.AddTransport(ClientATransport, new TestEntity());
            Server.AddTransport(ClientBTransport, new TestEntity());
            ClientA.Connect(serverATransport);
            ClientB.Connect(serverBTransport);
        }


        [Test]
        public void Test_EntitySpawning()
        {
            TestEntity testEntityA = new TestEntity();
            testEntityA.TestInt.Value = 123;

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

            Assert.AreEqual(testEntityA.TestInt.Value, testEntityServer.TestInt.Value);
            Assert.AreEqual(testEntityA.TestInt.Value, testEntityB.TestInt.Value);

            ClientA.Despawn(testEntityA);
            ClientA.Tick();
            Server.Tick();
            ClientB.Tick();
            Assert.IsFalse(ServerEntityStorage.TryGetEntityByNetworkID(testEntityA.NetworkID, out _));
            Assert.IsFalse(ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out _));

        }

        [Test]
        public void Test_OwnerEntityModification()
        {
            TestEntity testEntityA = new TestEntity();

            ClientA.Spawn(testEntityA);
            ClientA.Tick();
            Server.Tick();
            ClientB.Tick();

            ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB);
            ServerEntityStorage.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityServer);

            TestEntity testEntityServer = (TestEntity)entityServer;
            TestEntity testEntityB = (TestEntity)entityB;

            testEntityA.TestInt.Value = 123;

            ClientA.Tick();
            Server.Tick();
            ClientB.Tick();
           
            // Value was replicated to B and Server
            Assert.AreEqual(123, testEntityA.TestInt.Value);
            Assert.AreEqual(123, testEntityServer.TestInt.Value);
            Assert.AreEqual(123, testEntityB.TestInt.Value);

        }

        [Test]
        public void Test_NonOwnerEntityModification()
        {
            TestEntity testEntityA = new TestEntity();

            ClientA.Spawn(testEntityA);
            ClientA.Tick();
            Server.Tick();
            ClientB.Tick();

            ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB);
            ServerEntityStorage.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityServer);

            TestEntity testEntityServer = (TestEntity)entityServer;
            TestEntity testEntityB = (TestEntity)entityB;

            testEntityB.TestInt.Value = 123;

            ClientB.Tick();
            Server.Tick();
            ClientA.Tick();

            // Values are not replicated to A or Server
            Assert.AreEqual(0, testEntityA.TestInt.Value);
            Assert.AreEqual(0, testEntityServer.TestInt.Value);

            // Value remains on non-owner side.
            Assert.AreEqual(123, testEntityB.TestInt.Value);

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

			Assert.IsFalse(testEntityA.IsOwner);
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

            testEntityA.DestroyWithOwner.Value = true;

			ClientA.Tick();
			Server.Tick(); // Server will not read packets from A if it's transport was removed first.

			Server.RemoveTransport(ClientATransport);
			Server.Tick();
			ClientB.Tick();

			Assert.IsFalse(ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB));
		}


		[Test]
        public void Test_EntityRPC()
        {
            TestEntity testEntityA = new TestEntity();
            testEntityA.TestInt.Value = 123;

            ClientA.Spawn(testEntityA);
            ClientA.Tick();
            Server.Tick();
            ClientB.Tick();

            ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB);
            TestEntity testEntityB = (TestEntity)entityB;
            ServerEntityStorage.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityServer);
            TestEntity testEntityServer = (TestEntity)entityServer;

            Assert.AreEqual(123, testEntityB.TestInt.Value);
            Assert.AreEqual(123, testEntityServer.TestInt.Value);

            testEntityB.Increment();
            testEntityB.Add(5);

            Assert.AreEqual(123, testEntityB.TestInt.Value);
            Assert.AreEqual(123, testEntityServer.TestInt.Value);

            ClientB.Tick();
            Server.Tick();
            ClientB.Tick();

            // RPC was not executed on either B or Server
            Assert.AreEqual(123, testEntityA.TestInt.Value);
            Assert.AreEqual(123, testEntityB.TestInt.Value);
            Assert.AreEqual(123, testEntityServer.TestInt.Value);

            ClientA.Tick();

            // RPC executed on A
            Assert.AreEqual(129, testEntityA.TestInt.Value);

            // Values not yet replicated to B or Server
            Assert.AreEqual(123, testEntityB.TestInt.Value);
            Assert.AreEqual(123, testEntityServer.TestInt.Value);

            Server.Tick();
            ClientB.Tick();

            // Values replicated to B and Server
            Assert.AreEqual(129, testEntityB.TestInt.Value);
            Assert.AreEqual(129, testEntityServer.TestInt.Value);

            // Ensure RPCs are executed locally for owning clients
            testEntityA.Add(5);
			Assert.AreEqual(134, testEntityA.TestInt.Value);
			Assert.AreEqual(129, testEntityB.TestInt.Value);

			ClientA.Tick();
			Server.Tick();
			ClientA.Tick(); // To be sure we don't double execute
			ClientB.Tick();

			Assert.AreEqual(134, testEntityA.TestInt.Value);
			Assert.AreEqual(134, testEntityB.TestInt.Value);

		}



		[Test]
		public void Test_EntityMulticast() {

			TestEntity testEntityA = new TestEntity();

			ClientA.Spawn(testEntityA);
			ClientA.Tick();
			Server.Tick();
			ClientB.Tick();

			ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB);
			TestEntity testEntityB = (TestEntity)entityB;
			ServerEntityStorage.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityServer);
			TestEntity testEntityServer = (TestEntity)entityServer;

            Assert.IsFalse(testEntityA.MulticastExecuted);
			Assert.IsFalse(testEntityServer.MulticastExecuted);
			Assert.IsFalse(testEntityB.MulticastExecuted);

            testEntityA.InvokeTestMulticast();

			ClientA.Tick();
			Server.Tick();
			ClientB.Tick();

			Assert.IsFalse(testEntityA.MulticastExecuted);
			Assert.IsFalse(testEntityServer.MulticastExecuted);
			Assert.IsTrue(testEntityB.MulticastExecuted);

            // Ensure non-owners cannot invoke multicasts
            Assert.Throws<InvalidOperationException>(() => {
				testEntityB.InvokeTestMulticast();
			});
		}


	}
}