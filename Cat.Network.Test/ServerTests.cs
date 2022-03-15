using NUnit.Framework;

namespace Cat.Network.Test
{
    public class ServerTests
    {
        private Server Server { get; set; }
        private TestEntityStorage ServerEntityStorage { get; set; }
        
        private Client ClientA { get; set; }
        private Client ClientB { get; set; }


        [SetUp]
        public void Setup()
        {
            ServerEntityStorage = new TestEntityStorage();
            Server = new Server(ServerEntityStorage);
            ClientA = new Client();
            ClientB = new Client();

            TestTransport clientATransport = new TestTransport();
            TestTransport clientBTransport = new TestTransport();
            TestTransport serverATransport = new TestTransport();
            TestTransport serverBTransport = new TestTransport();

            clientATransport.Remote = serverATransport;
            serverATransport.Remote = clientATransport;
            clientBTransport.Remote = serverBTransport;
            serverBTransport.Remote = clientBTransport;

            Server.AddTransport(clientATransport);
            Server.AddTransport(clientBTransport);
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

            Assert.AreEqual(0, testEntityA.TestInt.Value);
            Assert.AreEqual(0, testEntityServer.TestInt.Value);
            Assert.AreEqual(123, testEntityB.TestInt.Value);

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
            ClientA.Tick();
            Server.Tick();
            ClientB.Tick();

            Assert.AreEqual(129, testEntityB.TestInt.Value);
            Assert.AreEqual(129, testEntityServer.TestInt.Value);
        }


    }
}