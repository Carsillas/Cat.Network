using System;
using System.Numerics;
using NUnit.Framework;

namespace Cat.Network.Test.Serialization;

public class ReferenceTypeSerializationTests : CatNetworkTest {
	
	[Test]
	public void Test_ReferenceTypeReplacement() {
		ReferenceTypeTestEntity testEntityA = new ReferenceTypeTestEntity {
			
		};

		ClientA.Spawn(testEntityA);

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();

		Assert.IsTrue(ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB));
		ReferenceTypeTestEntity testEntityB = (ReferenceTypeTestEntity)entityB;

		Assert.IsNull(testEntityA.Test);
		Assert.IsNull(testEntityB.Test);

		testEntityA.Test = new CustomNetworkDataObject {
			Test = 7
		};

		ClientA.Tick();
		Server.Tick();
		ClientB.Tick();
		
		Assert.IsNotNull(testEntityA.Test);
		Assert.IsNotNull(testEntityB.Test);

		Assert.AreNotSame(testEntityA.Test, testEntityB.Test);
		Assert.AreSame(testEntityB.Test.GetType(), typeof(CustomNetworkDataObject));
		Assert.AreEqual(7, ((CustomNetworkDataObject)testEntityB.Test).Test);
		
	}
}