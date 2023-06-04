using Cat.Network.Test.Server;
using NUnit.Framework;

namespace Cat.Network.Test.Serialization;
public class SerializationTests : CatNetworkTest {

	private const string WowString = "Wow!";

	[Test]
	public void Test_NetworkPropertySerialization() {
		SerializationTestEntity testEntityA = new SerializationTestEntity {
			BooleanProperty = true,
			ByteProperty = 10,
			ShortProperty = 20,
			IntProperty = 30,
			LongProperty = 40,
			UShortProperty = 50,
			UIntProperty = 60,
			ULongProperty = 70,
			StringProperty = WowString
		};

		ClientA.Spawn(testEntityA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		Assert.IsTrue(ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB));
		SerializationTestEntity testEntityB = (SerializationTestEntity)entityB;


		Assert.AreEqual(testEntityA.BooleanProperty, testEntityB.BooleanProperty);
		Assert.AreEqual(testEntityA.ByteProperty, testEntityB.ByteProperty);
		Assert.AreEqual(testEntityA.ShortProperty, testEntityB.ShortProperty);
		Assert.AreEqual(testEntityA.IntProperty, testEntityB.IntProperty);
		Assert.AreEqual(testEntityA.LongProperty, testEntityB.LongProperty);
		Assert.AreEqual(testEntityA.UShortProperty, testEntityB.UShortProperty);
		Assert.AreEqual(testEntityA.UIntProperty, testEntityB.UIntProperty);
		Assert.AreEqual(testEntityA.ULongProperty, testEntityB.ULongProperty);
		Assert.AreEqual(testEntityA.StringProperty, testEntityB.StringProperty);

	}

	[Test]
	public void Test_NetworkPropertySetCount() {
		SerializationTestEntity testEntityA = new SerializationTestEntity {
			StringProperty = WowString
		};

		ClientA.Spawn(testEntityA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		Assert.IsTrue(ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB));
		SerializationTestEntity testEntityB = (SerializationTestEntity)entityB;

		Assert.AreEqual(testEntityA.StringProperty, testEntityB.StringProperty);
		Assert.AreEqual(1, testEntityB.StringSetCount);


		for (int i = 0; i < 10; i++) {
			testEntityB.TestMemoryRPC(
				testEntityB.BooleanProperty,
				testEntityB.ByteProperty,
				testEntityB.ShortProperty,
				testEntityB.IntProperty,
				testEntityB.LongProperty,
				testEntityB.UShortProperty,
				testEntityB.UIntProperty,
				testEntityB.ULongProperty);
			ClientB.Tick();
			Server.Tick();
			ClientA.Tick();
		}
		for (int i = 0; i < 10; i++) {
			ClientB.Tick();
			Server.Tick();
			ClientA.Tick();
		}

		Assert.AreEqual(1, testEntityB.StringSetCount);
		Assert.AreEqual("Wow!", testEntityB.StringProperty);

		testEntityA.StringProperty = "Wow!!!";

		for (int i = 0; i < 10; i++) {
			ClientB.Tick();
			Server.Tick();
			ClientA.Tick();
		}

		Assert.AreEqual(2, testEntityB.StringSetCount);
		Assert.AreEqual("Wow!!!", testEntityB.StringProperty);
	}


	[Test]
	public void Test_RPCSerialization() {
		SerializationTestEntity testEntityA = new SerializationTestEntity {
			BooleanProperty = true,
			ByteProperty = 10,
			ShortProperty = 20,
			IntProperty = 30,
			LongProperty = 40,
			UShortProperty = 50,
			UIntProperty = 60,
			ULongProperty = 70,
			StringProperty = WowString
		};

		ClientA.Spawn(testEntityA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		Assert.IsTrue(ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB));
		SerializationTestEntity testEntityB = (SerializationTestEntity)entityB;

		testEntityB.TestRPC(
			testEntityB.BooleanProperty,
			testEntityB.ByteProperty,
			testEntityB.ShortProperty,
			testEntityB.IntProperty,
			testEntityB.LongProperty,
			testEntityB.UShortProperty,
			testEntityB.UIntProperty,
			testEntityB.ULongProperty,
			testEntityB.StringProperty);

		ClientB.Tick();
		Server.Tick();
		ClientA.Tick();

		Assert.IsTrue(testEntityA.RPCInvoked);
		Assert.IsFalse(testEntityB.RPCInvoked);
	}


	[Test]
	public void Test_CollectionSerialization() {
		SerializationTestEntity testEntityA = new SerializationTestEntity {	};

		ClientA.Spawn(testEntityA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		Assert.IsTrue(ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB));
		SerializationTestEntity testEntityB = (SerializationTestEntity)entityB;


		ClientB.Tick();
		Server.Tick();
		ClientA.Tick();


		testEntityA.MyInts.Add(1);
		testEntityA.MyInts.Add(2);
		testEntityA.MyInts.Add(6);
		testEntityA.MyInts.Remove(2);
		testEntityA.BooleanProperty = true;

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		CollectionAssert.AreEqual(testEntityA.MyInts, testEntityB.MyInts);

		testEntityA.MyInts.Add(7);
		testEntityA.BooleanProperty = true;

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		CollectionAssert.AreEqual(testEntityA.MyInts, testEntityB.MyInts);

	}
}
