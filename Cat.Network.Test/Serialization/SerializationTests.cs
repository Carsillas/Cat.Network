using System;
using Cat.Network.Test.Server;
using NUnit.Framework;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

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
			FloatProperty = 123.456f,
			DoubleProperty = 789.123,
			StringProperty = WowString,
			EnumProperty = CustomEnum.Test1,
			GuidProperty = Guid.NewGuid(),
			NullableGuidProperty = Guid.NewGuid(),
			VectorProperty = new Vector3(1, 2, 3),
			ImmutableArrayProperty = ImmutableArray.Create<byte>(1, 2, 3, 4, 5, 6, 7, 8, 9, 0)
		};

		ClientA.Spawn(testEntityA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		Assert.IsTrue(ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityB));
		SerializationTestEntity testEntityB = (SerializationTestEntity)entityB;


		Assert.AreEqual(testEntityA.BooleanProperty, testEntityB.BooleanProperty);
		Assert.AreEqual(testEntityA.ByteProperty, testEntityB.ByteProperty);
		Assert.AreEqual(testEntityA.ShortProperty, testEntityB.ShortProperty);
		Assert.AreEqual(testEntityA.IntProperty, testEntityB.IntProperty);
		Assert.AreEqual(testEntityA.LongProperty, testEntityB.LongProperty);
		Assert.AreEqual(testEntityA.UShortProperty, testEntityB.UShortProperty);
		Assert.AreEqual(testEntityA.UIntProperty, testEntityB.UIntProperty);
		Assert.AreEqual(testEntityA.ULongProperty, testEntityB.ULongProperty);
		Assert.AreEqual(testEntityA.FloatProperty, testEntityB.FloatProperty);
		Assert.AreEqual(testEntityA.DoubleProperty, testEntityB.DoubleProperty);
		Assert.AreEqual(testEntityA.StringProperty, testEntityB.StringProperty);
		Assert.AreEqual(testEntityA.EnumProperty, testEntityB.EnumProperty);
		Assert.AreEqual(testEntityA.GuidProperty, testEntityB.GuidProperty);
		Assert.AreEqual(testEntityA.NullableGuidProperty, testEntityB.NullableGuidProperty);
		Assert.AreEqual(testEntityA.VectorProperty, testEntityB.VectorProperty);
		
		CollectionAssert.AreEqual(testEntityA.ImmutableArrayProperty, testEntityB.ImmutableArrayProperty);

	}

	
	[Test]
	public void Test_RawSerializeDeserialize() {
		SerializationTestEntity testEntityA = new SerializationTestEntity {
			BooleanProperty = true,
			ByteProperty = 10,
			ShortProperty = 20,
			IntProperty = 30,
			LongProperty = 40,
			UShortProperty = 50,
			UIntProperty = 60,
			ULongProperty = 70,
			FloatProperty = 123.456f,
			DoubleProperty = 789.123,
			StringProperty = null,
			EnumProperty = CustomEnum.Test1,
			GuidProperty = Guid.NewGuid(),
			NullableGuidProperty = Guid.NewGuid(),
			VectorProperty = new Vector3(1, 2, 3)
		};

		ClientA.Spawn(testEntityA);

		byte[] buffer = new byte[10_000];
		int contentLength = SerializationUtils.Serialize(testEntityA, buffer);

		NetworkEntity deserializedEntity = SerializationUtils.Deserialize(new ReadOnlySpan<byte>(buffer, 0, contentLength));
		Assert.IsInstanceOf<SerializationTestEntity>(deserializedEntity);
		SerializationTestEntity deserializedTestEntity = (SerializationTestEntity)deserializedEntity;
		
		Assert.AreEqual(testEntityA.NetworkId, deserializedTestEntity.NetworkId);
		Server.Spawn(deserializedTestEntity);
		Assert.AreEqual(testEntityA.NetworkId, deserializedTestEntity.NetworkId);
		
		ClientA.Despawn(testEntityA);
		
		Assert.AreEqual(testEntityA.BooleanProperty, deserializedTestEntity.BooleanProperty);
		Assert.AreEqual(testEntityA.ByteProperty, deserializedTestEntity.ByteProperty);
		Assert.AreEqual(testEntityA.ShortProperty, deserializedTestEntity.ShortProperty);
		Assert.AreEqual(testEntityA.IntProperty, deserializedTestEntity.IntProperty);
		Assert.AreEqual(testEntityA.LongProperty, deserializedTestEntity.LongProperty);
		Assert.AreEqual(testEntityA.UShortProperty, deserializedTestEntity.UShortProperty);
		Assert.AreEqual(testEntityA.UIntProperty, deserializedTestEntity.UIntProperty);
		Assert.AreEqual(testEntityA.ULongProperty, deserializedTestEntity.ULongProperty);
		Assert.AreEqual(testEntityA.FloatProperty, deserializedTestEntity.FloatProperty);
		Assert.AreEqual(testEntityA.DoubleProperty, deserializedTestEntity.DoubleProperty);
		Assert.AreEqual(testEntityA.StringProperty, deserializedTestEntity.StringProperty);
		Assert.AreEqual(testEntityA.EnumProperty, deserializedTestEntity.EnumProperty);
		Assert.AreEqual(testEntityA.GuidProperty, deserializedTestEntity.GuidProperty);
		Assert.AreEqual(testEntityA.NullableGuidProperty, deserializedTestEntity.NullableGuidProperty);
		Assert.AreEqual(testEntityA.VectorProperty, deserializedTestEntity.VectorProperty);
		
		Server.Tick();
		// Expecting a create entity request and an assign owner entity request
		Assert.AreEqual(2, ClientATransport.Remote.Messages.Count);
		
		ClientA.Tick();
		Assert.AreEqual(0, ClientATransport.Messages.Count);
		
		Server.Tick();
		Assert.AreEqual(0, ClientATransport.Remote.Messages.Count);
		
		ClientA.Tick();
		Assert.AreEqual(0, ClientATransport.Messages.Count);
		
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

		Assert.IsTrue(ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityB));
		SerializationTestEntity testEntityB = (SerializationTestEntity)entityB;

		Assert.AreEqual(testEntityA.StringProperty, testEntityB.StringProperty);
		Assert.AreEqual(1, testEntityB.StringSetCount);


		for (int i = 0; i < 10; i++) {
			testEntityB.TestMemoryRpc(
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
	public void Test_RpcSerialization() {
		SerializationTestEntity testEntityA = new SerializationTestEntity {
			BooleanProperty = true,
			ByteProperty = 10,
			ShortProperty = 20,
			IntProperty = 30,
			LongProperty = 40,
			UShortProperty = 50,
			UIntProperty = 60,
			ULongProperty = 70,
			StringProperty = WowString,
			TransformProperty = new Transform {
				Position = Vector3.One,
				Scale = Vector3.One * 2,
				TestString = "Wow!"
			}
		};

		ClientA.Spawn(testEntityA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		Assert.IsTrue(ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityB));
		SerializationTestEntity testEntityB = (SerializationTestEntity)entityB;

		testEntityB.TestRpc(
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

		testEntityB.TestTransformSerialization(new Transform {
			Position = Vector3.One,
			Scale = Vector3.One * 2,
			TestString = "Wow!"
		});

		ClientB.Tick();
		Server.Tick();
		ClientA.Tick();

		Assert.IsTrue(testEntityA.RpcInvoked);
		Assert.IsFalse(testEntityB.RpcInvoked);
	}


	[Test]
	public void Test_CollectionSerialization() {
		SerializationTestEntity testEntityA = new SerializationTestEntity {	};

		ClientA.Spawn(testEntityA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		Assert.IsTrue(ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityB));
		SerializationTestEntity testEntityB = (SerializationTestEntity)entityB;


		testEntityA.MyInts.Add(1);
		testEntityA.MyInts.Add(2);
		testEntityA.MyInts.Add(6);
		testEntityA.MyInts.Remove(2);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		CollectionAssert.AreEqual(testEntityA.MyInts, testEntityB.MyInts);

		testEntityA.MyInts.Add(7);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		CollectionAssert.AreEqual(testEntityA.MyInts, testEntityB.MyInts);


		testEntityA.MyInts.Clear();
		testEntityA.MyInts.Add(4);
		testEntityA.MyInts.Add(46);
		testEntityA.MyInts.Add(35235);
		testEntityA.MyInts[2] = 4;

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		ClientA.Despawn(testEntityA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		testEntityA.MyInts.Add(19);
		testEntityA.MyInts.Add(20);

		ClientA.Spawn(testEntityA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		Assert.IsTrue(ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out entityB));
		Assert.AreNotSame(testEntityB, entityB); // testEntityA was respawned, should not be the same instance
		testEntityB = (SerializationTestEntity)entityB;

		CollectionAssert.AreEqual(testEntityA.MyInts, testEntityB.MyInts);

	}

	[Test]
	public void Test_NetworkDataObjectCollectionSerialization() {
		ReferenceTypeTestEntity testEntityA = new ReferenceTypeTestEntity {	};

		ClientA.Spawn(testEntityA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		Assert.IsTrue(ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityB));
		ReferenceTypeTestEntity testEntityB = (ReferenceTypeTestEntity)entityB;


		testEntityA.Inventory.Add(null);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		CollectionAssert.AreEqual(testEntityA.Inventory, testEntityB.Inventory);

		CustomNetworkDataObject itemA = new CustomNetworkDataObject();
		
		testEntityA.Inventory.Add(itemA);
		testEntityA.Inventory[1] = null;
		testEntityA.Inventory.Add(itemA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();
		
		Assert.IsNull(testEntityB.Inventory[0]);
		Assert.IsNull(testEntityB.Inventory[1]);
		Assert.AreEqual(0, testEntityB.Inventory[2].Test);
		
		testEntityA.Inventory.Swap(1, 2);
		
		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();
		
		
		Assert.IsNull(testEntityA.Inventory[0]);
		Assert.AreEqual(0, testEntityA.Inventory[1].Test);
		Assert.IsNull(testEntityA.Inventory[2]);
		Assert.AreEqual(3, testEntityA.Inventory.Count);
		
		
		Assert.IsNull(testEntityB.Inventory[0]);
		Assert.AreEqual(0, testEntityB.Inventory[1].Test);
		Assert.IsNull(testEntityB.Inventory[2]);
		Assert.AreEqual(3, testEntityB.Inventory.Count);
	}

	
	[Test]
	public void Test_NullableSerialization() {
		SerializationTestEntity testEntityA = new SerializationTestEntity { NullableIntProperty = 14 };

		ClientA.Spawn(testEntityA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		Assert.IsTrue(ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityB));
		SerializationTestEntity testEntityB = (SerializationTestEntity)entityB;


		Assert.AreEqual(testEntityA.NullableIntProperty, testEntityB.NullableIntProperty);

		testEntityA.NullableIntProperty = null;
		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();
		Assert.AreEqual(testEntityA.NullableIntProperty, testEntityB.NullableIntProperty);

		testEntityA.NullableIntProperty = 156;
		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();
		Assert.AreEqual(testEntityA.NullableIntProperty, testEntityB.NullableIntProperty);


		testEntityA.NullableIntProperty = -156;
		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();
		Assert.AreEqual(testEntityA.NullableIntProperty, testEntityB.NullableIntProperty);


		testEntityA.NullableIntProperty = null;
		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();
		Assert.AreEqual(testEntityA.NullableIntProperty, testEntityB.NullableIntProperty);
	}
	
	[Test]
	public void Test_PropertyChangedEvent() {
		SerializationTestEntity testEntityA = new SerializationTestEntity { ByteProperty = 14 };

		ClientA.Spawn(testEntityA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		Assert.IsTrue(ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityB));
		SerializationTestEntity testEntityB = (SerializationTestEntity)entityB;

		bool eventInvokedA = false;
		bool eventInvokedB = false;
		
		testEntityA.BytePropertyChanged += (sender, args) => {
			eventInvokedA = true;
		};
		
		testEntityB.BytePropertyChanged += (sender, args) => {
			eventInvokedB = true;
		};
		
		testEntityA.ByteProperty = 12;
		
		Assert.IsTrue(eventInvokedA);
		Assert.IsFalse(eventInvokedB);
		
		eventInvokedA = false;
		eventInvokedB = false;
		
		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();
		
		Assert.IsFalse(eventInvokedA);
		Assert.IsTrue(eventInvokedB);
		
		eventInvokedA = false;
		eventInvokedB = false;
		
		testEntityA.ByteProperty = 12;
		
		Assert.IsFalse(eventInvokedA);
		Assert.IsFalse(eventInvokedB);
		
		eventInvokedA = false;
		eventInvokedB = false;
		
		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();
		
		Assert.IsFalse(eventInvokedA);
		Assert.IsFalse(eventInvokedB);
		
		eventInvokedA = false;
		eventInvokedB = false;
		
		testEntityA.ByteProperty = 13;
		
		Assert.IsTrue(eventInvokedA);
		Assert.IsFalse(eventInvokedB);
		
		eventInvokedA = false;
		eventInvokedB = false;
		
		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();
		
		Assert.IsFalse(eventInvokedA);
		Assert.IsTrue(eventInvokedB);
		
	}

}
