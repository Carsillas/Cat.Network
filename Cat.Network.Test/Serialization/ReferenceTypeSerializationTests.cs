using System;
using System.Numerics;
using NUnit.Framework;

namespace Cat.Network.Test.Serialization; 

public class ReferenceTypeSerializationTests : CatNetworkTest {

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
			VectorProperty = new Vector3(1, 2, 3)
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
		Assert.AreEqual(testEntityA.FloatProperty, testEntityB.FloatProperty);
		Assert.AreEqual(testEntityA.DoubleProperty, testEntityB.DoubleProperty);
		Assert.AreEqual(testEntityA.StringProperty, testEntityB.StringProperty);
		Assert.AreEqual(testEntityA.EnumProperty, testEntityB.EnumProperty);
		Assert.AreEqual(testEntityA.GuidProperty, testEntityB.GuidProperty);
		Assert.AreEqual(testEntityA.NullableGuidProperty, testEntityB.NullableGuidProperty);
		Assert.AreEqual(testEntityA.VectorProperty, testEntityB.VectorProperty);

	}
}