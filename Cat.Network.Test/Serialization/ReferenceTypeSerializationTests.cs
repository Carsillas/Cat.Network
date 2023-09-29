using System;
using System.Numerics;
using NUnit.Framework;

namespace Cat.Network.Test.Serialization;

public class ReferenceTypeSerializationTests : CatNetworkTest {
	[Test]
	public void Test_ReferenceTypeReplacement() {
		ReferenceTypeTestEntity testEntityA = new ReferenceTypeTestEntity { };

		ClientA.Spawn(testEntityA);

		Cycle();

		Assert.IsTrue(ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB));
		ReferenceTypeTestEntity testEntityB = (ReferenceTypeTestEntity)entityB;

		Assert.IsNull(testEntityA.Test);
		Assert.IsNull(testEntityB.Test);

		testEntityA.Test = new CustomNetworkDataObject {
			Test = 7
		};

		Cycle();

		Assert.IsNotNull(testEntityA.Test);
		Assert.IsNotNull(testEntityB.Test);

		Assert.AreNotSame(testEntityA.Test, testEntityB.Test);
		Assert.AreSame(testEntityB.Test.GetType(), typeof(CustomNetworkDataObject));
		Assert.AreEqual(7, ((CustomNetworkDataObject)testEntityB.Test).Test);


		testEntityA.Test = null;

		Cycle();
		
		Assert.IsNull(testEntityA.Test);
		Assert.IsNull(testEntityB.Test);
	}

	[Test]
	public void Test_ReferenceTypeUpdate() {
		ReferenceTypeTestEntity testEntityA = new ReferenceTypeTestEntity {
			Test = new CustomNetworkDataObject {
				Test = 7
			}
		};

		ClientA.Spawn(testEntityA);

		Cycle();

		Assert.IsTrue(
			Server.EntityStorage.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityServer));
		Assert.IsTrue(ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB));
		ReferenceTypeTestEntity testEntityServer = (ReferenceTypeTestEntity)entityServer;
		ReferenceTypeTestEntity testEntityB = (ReferenceTypeTestEntity)entityB;

		Assert.IsNotNull(testEntityA.Test);
		Assert.IsNotNull(testEntityB.Test);

		NetworkDataObject networkDataObjectServer = testEntityServer.Test;
		NetworkDataObject networkDataObjectB = testEntityB.Test;

		Assert.AreNotSame(testEntityA.Test, testEntityServer.Test);
		Assert.AreNotSame(testEntityA.Test, testEntityB.Test);

		Assert.AreSame(testEntityServer.Test.GetType(), typeof(CustomNetworkDataObject));
		Assert.AreSame(testEntityB.Test.GetType(), typeof(CustomNetworkDataObject));

		Assert.AreEqual(7, ((CustomNetworkDataObject)testEntityServer.Test).Test);
		Assert.AreEqual(7, ((CustomNetworkDataObject)testEntityB.Test).Test);

		((CustomNetworkDataObject)testEntityA.Test).Test = 8;

		Cycle();

		Assert.AreNotSame(testEntityA.Test, testEntityServer.Test);
		Assert.AreNotSame(testEntityA.Test, testEntityB.Test);
		Assert.AreSame(networkDataObjectServer, testEntityServer.Test);
		Assert.AreSame(networkDataObjectB, testEntityB.Test);
		Assert.AreEqual(8, ((CustomNetworkDataObject)testEntityServer.Test).Test);
		Assert.AreEqual(8, ((CustomNetworkDataObject)testEntityB.Test).Test);
	}


	[Test]
	public void Test_ReferenceTypeNonOwnerModification() {
		ReferenceTypeTestEntity testEntityA = new ReferenceTypeTestEntity {
			Test = new CustomNetworkDataObject {
				Test = 7
			}
		};

		ClientA.Spawn(testEntityA);

		Cycle();

		Assert.IsTrue(
			Server.EntityStorage.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityServer));
		Assert.IsTrue(ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB));
		ReferenceTypeTestEntity testEntityServer = (ReferenceTypeTestEntity)entityServer;
		ReferenceTypeTestEntity testEntityB = (ReferenceTypeTestEntity)entityB;

		NetworkDataObject networkDataObjectServer = testEntityServer.Test;
		NetworkDataObject networkDataObjectB = testEntityB.Test;

		((CustomNetworkDataObject)testEntityB.Test).Test = 8;
		
		Cycle();

		Assert.AreNotSame(testEntityA.Test, testEntityServer.Test);
		Assert.AreNotSame(testEntityA.Test, testEntityB.Test);
		Assert.AreSame(networkDataObjectServer, testEntityServer.Test);
		Assert.AreSame(networkDataObjectB, testEntityB.Test);
		Assert.AreEqual(7, ((CustomNetworkDataObject)testEntityServer.Test).Test);
		// TODO? Assert.AreEqual(7, ((CustomNetworkDataObject)testEntityB.Test).Test);
	}

	[Test]
	public void Test_ReferenceTypeCollectionSerialization() {
		ReferenceTypeTestEntity testEntityA = new ReferenceTypeTestEntity();

		testEntityA.Inventory.Add(new CustomNetworkDataObject { Test = 10 });


		ClientA.Spawn(testEntityA);

		Cycle();

		Assert.IsTrue(Server.EntityStorage.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityServer));
		Assert.IsTrue(ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB));
		ReferenceTypeTestEntity testEntityServer = (ReferenceTypeTestEntity)entityServer;
		ReferenceTypeTestEntity testEntityB = (ReferenceTypeTestEntity)entityB;

		Assert.AreEqual(1, testEntityB.Inventory.Count);
		Assert.IsInstanceOf<CustomNetworkDataObject>(testEntityB.Inventory[0]);
		Assert.AreEqual(10, testEntityB.Inventory[0].Test);
	}
}