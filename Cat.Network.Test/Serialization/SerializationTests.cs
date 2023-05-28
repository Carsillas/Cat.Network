using NUnit.Framework;
using System;
using System.Globalization;

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
	public void Test_RPCSerializationMemory() {
		SerializationTestEntity testEntityA = new SerializationTestEntity {
			BooleanProperty = true,
			ByteProperty = 10,
			ShortProperty = 20,
			IntProperty = 30,
			LongProperty = 40,
			UShortProperty = 50,
			UIntProperty = 60,
			ULongProperty = 70
		};

		ClientA.Spawn(testEntityA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		Assert.IsTrue(ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB));
		SerializationTestEntity testEntityB = (SerializationTestEntity)entityB;

		ExecuteRPCAndVerifyParameters(testEntityA, testEntityB);
		ExecuteRPCAndVerifyParameters(testEntityA, testEntityB);
		ExecuteRPCAndVerifyParameters(testEntityA, testEntityB);
		ExecuteRPCAndVerifyParameters(testEntityA, testEntityB);
		ExecuteRPCAndVerifyParameters(testEntityA, testEntityB);
		ExecuteRPCAndVerifyParameters(testEntityA, testEntityB);
		MemoryTracker memory = new MemoryTracker();
		ExecuteRPCAndVerifyParameters(testEntityA, testEntityB);
		ExecuteRPCAndVerifyParameters(testEntityA, testEntityB);
		ExecuteRPCAndVerifyParameters(testEntityA, testEntityB);
		ExecuteRPCAndVerifyParameters(testEntityA, testEntityB);
		ExecuteRPCAndVerifyParameters(testEntityA, testEntityB);
		memory.Dispose();
	}


	private void ExecuteRPCAndVerifyParameters(SerializationTestEntity testEntityA, SerializationTestEntity testEntityB) {

		testEntityB.TestMemoryRPC(
			testEntityB.BooleanProperty,
			testEntityB.ByteProperty,
			testEntityB.ShortProperty,
			testEntityB.IntProperty,
			testEntityB.LongProperty,
			testEntityB.UShortProperty,
			testEntityB.UIntProperty,
			testEntityB.ULongProperty);

		MemoryTracker memory = new MemoryTracker();
		ClientB.Tick();
		Server.Tick();
		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();
		memory.Dispose();

		Assert.IsTrue(testEntityA.RPCInvoked);
		Assert.IsFalse(testEntityB.RPCInvoked);

	}

}