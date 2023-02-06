using Cat.Network.Entities;
using NUnit.Framework;
using System;


namespace Cat.Network.Test.NetworkPropertyTests {

	public class NetworkPropertyTest : CatNetworkTest {

		[Test]
		public void Test_FieldNetworkPropertyThrows() {
			Assert.Throws<Exception>(() => new FieldNetworkPropertyEntity1());
			Assert.Throws<Exception>(() => new FieldNetworkPropertyEntity2());
			Assert.Throws<Exception>(() => new FieldNetworkPropertyEntity3());
		}

		[Test]
		public void Test_OwnerEntityModification() {
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
		public void Test_NonOwnerEntityModification() {
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
		public void Test_CreateSyncOnly() {

			TestEntity testEntityA = new TestEntity();

			testEntityA.TestIntCreateOnly.Value = 123;

			ClientA.Spawn(testEntityA);

			ClientA.Tick();
			Server.Tick();
			ClientB.Tick();

			ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB);
			TestEntity testEntityB = (TestEntity)entityB;

			Assert.AreEqual(123, testEntityB.TestIntCreateOnly.Value);

			testEntityA.TestIntCreateOnly.Value = 100;

			ClientA.Tick();
			Server.Tick();
			ClientB.Tick();

			Assert.AreEqual(123, testEntityB.TestIntCreateOnly.Value);

			var (ClientC, ClientCTransport, ProxyManagerC) = AddClient();

			Server.Tick();
			ClientC.Tick();

			ClientC.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityC);
			TestEntity testEntityC = (TestEntity)entityC;


			Assert.AreEqual(123, testEntityB.TestIntCreateOnly.Value);
			Assert.AreEqual(123, testEntityC.TestIntCreateOnly.Value);

			testEntityA.InvokeIncrementTestIntCreateOnly();

			ClientA.Tick();
			Server.Tick();
			ClientB.Tick();
			ClientC.Tick();

			Assert.AreEqual(101, testEntityA.TestIntCreateOnly.Value);
			Assert.AreEqual(124, testEntityB.TestIntCreateOnly.Value);
			Assert.AreEqual(124, testEntityC.TestIntCreateOnly.Value);

		}


		[Test]
		public void Test_EnumSync() {

			TestEntity testEntityA = new TestEntity();

			testEntityA.TestEnum.Value = Test.Meow;

			ClientA.Spawn(testEntityA);
			ClientA.Tick();
			Server.Tick();
			ClientB.Tick();

			ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB);
			ServerEntityStorage.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityServer);

			TestEntity testEntityServer = (TestEntity)entityServer;
			TestEntity testEntityB = (TestEntity)entityB;

			Assert.AreEqual(Test.Meow, testEntityServer.TestEnum.Value);
			Assert.AreEqual(Test.Meow, testEntityB.TestEnum.Value);

			testEntityA.TestEnum.Value = Test.Woof;

			ClientA.Tick();
			Server.Tick();
			ClientB.Tick();

			Assert.AreEqual(Test.Woof, testEntityServer.TestEnum.Value);
			Assert.AreEqual(Test.Woof, testEntityB.TestEnum.Value);

		}

		[Test]
		public void Test_EntityReferenceSync() {

			TestEntity testEntityA = new TestEntity();

			testEntityA.TestEntityReference.Value = testEntityA;

			ClientA.Spawn(testEntityA);
			ClientA.Tick();
			Server.Tick();
			ClientB.Tick();

			ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB);
			ServerEntityStorage.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityServer);

			TestEntity testEntityServer = (TestEntity)entityServer;
			TestEntity testEntityB = (TestEntity)entityB;

			Assert.AreSame(testEntityServer, testEntityServer.TestEntityReference.Value);
			Assert.AreSame(testEntityB, testEntityB.TestEntityReference.Value);

		}


		[Test]
		public void Test_CompoundProperty() {

			TestEntity testEntityA = new TestEntity();

			testEntityA.TestCompound.Value.A.Value = 123;
			testEntityA.TestCompound.Value.B.Value = 234;

			ClientA.Spawn(testEntityA);
			ClientA.Tick();
			Server.Tick();
			ClientB.Tick();

			ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB);
			ServerEntityStorage.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityServer);

			TestEntity testEntityServer = (TestEntity)entityServer;
			TestEntity testEntityB = (TestEntity)entityB;

			Assert.AreEqual(123, testEntityServer.TestCompound.Value.A.Value);
			Assert.AreEqual(234, testEntityServer.TestCompound.Value.B.Value);
			Assert.AreEqual(123, testEntityB.TestCompound.Value.A.Value);
			Assert.AreEqual(234, testEntityB.TestCompound.Value.B.Value);

		}

	}
}