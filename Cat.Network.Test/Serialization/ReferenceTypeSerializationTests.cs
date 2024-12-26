using System;
using System.Numerics;
using Cat.Network.Collections;
using NUnit.Framework;

namespace Cat.Network.Test.Serialization;

public class ReferenceTypeSerializationTests : CatNetworkTest {
	[Test]
	public void Test_ReferenceTypeReplacement() {
		ReferenceTypeTestEntity testEntityA = new ReferenceTypeTestEntity { };

		ClientA.Spawn(testEntityA);

		Cycle();

		Assert.IsTrue(ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityB));
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
			Server.EntityStorage.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityServer));
		Assert.IsTrue(ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityB));
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
			Server.EntityStorage.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityServer));
		Assert.IsTrue(ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityB));
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
	public void Test_PreExistingCollectionSerialization() {
		ReferenceTypeTestEntity testEntityA = new ReferenceTypeTestEntity();

		testEntityA.Inventory.Add(new CustomNetworkDataObject { Test = 10 });

		ClientA.Spawn(testEntityA);

		Cycle();

		Assert.IsTrue(ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityB));
		ReferenceTypeTestEntity testEntityB = (ReferenceTypeTestEntity)entityB;

		Assert.AreEqual(1, testEntityB.Inventory.Count);
		Assert.IsInstanceOf<CustomNetworkDataObject>(testEntityB.Inventory[0]);
		Assert.AreEqual(10, testEntityB.Inventory[0].Test);
	}

	[Test]
	public void Test_CollectionAdditionSerialization() {
		ReferenceTypeTestEntity testEntityA = new ReferenceTypeTestEntity();

		ClientA.Spawn(testEntityA);

		Cycle();

		testEntityA.Inventory.Add(new CustomNetworkDataObject { Test = 10 });

		Cycle();

		Assert.IsTrue(ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityB));
		ReferenceTypeTestEntity testEntityB = (ReferenceTypeTestEntity)entityB;

		Assert.AreEqual(1, testEntityB.Inventory.Count);
		Assert.IsInstanceOf<CustomNetworkDataObject>(testEntityB.Inventory[0]);
		Assert.AreEqual(10, testEntityB.Inventory[0].Test);
	}

	[Test]
	public void Test_CollectionRemovalSerialization() {
		ReferenceTypeTestEntity testEntityA = new ReferenceTypeTestEntity();

		ClientA.Spawn(testEntityA);

		Cycle();

		testEntityA.Inventory.Add(new CustomNetworkDataObject { Test = 10 });

		Cycle();

		Assert.IsTrue(ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityB));
		ReferenceTypeTestEntity testEntityB = (ReferenceTypeTestEntity)entityB;

		Assert.AreEqual(1, testEntityB.Inventory.Count);
		Assert.IsInstanceOf<CustomNetworkDataObject>(testEntityB.Inventory[0]);
		Assert.AreEqual(10, testEntityB.Inventory[0].Test);

		testEntityA.Inventory.RemoveAt(0);

		Cycle();

		Assert.AreEqual(0, testEntityB.Inventory.Count);
	}

	[Test]
	public void Test_CollectionClearSerialization() {
		ReferenceTypeTestEntity testEntityA = new ReferenceTypeTestEntity();

		ClientA.Spawn(testEntityA);

		Cycle();

		testEntityA.Inventory.Add(new CustomNetworkDataObject { Test = 10 });

		Cycle();

		Assert.IsTrue(ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityB));
		ReferenceTypeTestEntity testEntityB = (ReferenceTypeTestEntity)entityB;

		Assert.AreEqual(1, testEntityB.Inventory.Count);
		Assert.IsInstanceOf<CustomNetworkDataObject>(testEntityB.Inventory[0]);
		Assert.AreEqual(10, testEntityB.Inventory[0].Test);

		testEntityA.Inventory.Clear();

		Cycle();

		Assert.AreEqual(0, testEntityB.Inventory.Count);
	}

	[Test]
	public void Test_CollectionUpdateSerialization() {
		ReferenceTypeTestEntity testEntityA = new ReferenceTypeTestEntity();

		ClientA.Spawn(testEntityA);
		testEntityA.Inventory.Add(new CustomNetworkDataObject { Test = 10 });

		Cycle();

		Assert.IsTrue(ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityB));
		ReferenceTypeTestEntity testEntityB = (ReferenceTypeTestEntity)entityB;

		Assert.AreEqual(1, testEntityB.Inventory.Count);
		Assert.IsInstanceOf<CustomNetworkDataObject>(testEntityB.Inventory[0]);
		Assert.AreEqual(10, testEntityB.Inventory[0].Test);
		CustomNetworkDataObject networkDataObjectB = testEntityB.Inventory[0];

		testEntityA.Inventory[0].Test = 20;

		Cycle();

		Assert.AreEqual(1, testEntityB.Inventory.Count);
		Assert.IsInstanceOf<CustomNetworkDataObject>(testEntityB.Inventory[0]);
		Assert.AreSame(networkDataObjectB, testEntityB.Inventory[0]);
		Assert.AreEqual(20, testEntityB.Inventory[0].Test);
	}


	[Test]
	public void Test_CollectionMultipleUpdatesSingleCollectionOperation() {
		ReferenceTypeTestEntity testEntityA = new ReferenceTypeTestEntity();

		ClientA.Spawn(testEntityA);
		testEntityA.Inventory.Add(new CustomNetworkDataObject { Test = 10 });

		Cycle();

		testEntityA.Inventory[0].Test = 20;
		testEntityA.Inventory[0].Test = 20;
		testEntityA.Inventory[0].Test = 20;
		testEntityA.Inventory[0].Test = 20;
		testEntityA.Inventory[0].Test = 20;
		testEntityA.Inventory[0].Test = 20;
		testEntityA.Inventory[0].Test = 20;

		INetworkCollection<CustomNetworkDataObject> collection = testEntityA.Inventory;

		Assert.AreEqual(1, collection.OperationBuffer.Count);

		Cycle();
	}

	[Test]
	public void Test_NetworkDataObjectCannotHaveMultipleOwners() {
		ReferenceTypeTestEntity testEntityA1 = new ReferenceTypeTestEntity();
		ReferenceTypeTestEntity testEntityA2 = new ReferenceTypeTestEntity();

		ClientA.Spawn(testEntityA1);
		CustomNetworkDataObject testNetworkDataObject = new CustomNetworkDataObject { Test = 10 };
		testEntityA1.Inventory.Add(testNetworkDataObject);
		Assert.Throws<InvalidOperationException>(() => testEntityA1.TestDerived = testNetworkDataObject);
		Assert.Throws<InvalidOperationException>(() => testEntityA2.Inventory.Add(testNetworkDataObject));
		Assert.Throws<InvalidOperationException>(() => testEntityA2.TestDerived = testNetworkDataObject);

		Assert.True(testEntityA1.Inventory.Remove(testNetworkDataObject));

		testEntityA1.TestDerived = testNetworkDataObject;
		Assert.Throws<InvalidOperationException>(() => testEntityA1.Inventory.Add(testNetworkDataObject));
		Assert.Throws<InvalidOperationException>(() => testEntityA2.Inventory.Add(testNetworkDataObject));
		Assert.Throws<InvalidOperationException>(() => testEntityA2.TestDerived = testNetworkDataObject);

		Cycle();

		Assert.IsTrue(ClientB.TryGetEntityByNetworkId(testEntityA1.NetworkId, out NetworkEntity entityB));
		ReferenceTypeTestEntity testEntityB = (ReferenceTypeTestEntity)entityB;

		Assert.AreEqual(10, testEntityB.TestDerived.Test);
		Assert.IsEmpty(testEntityB.Inventory);
	}

	[Test]
	public void Test_NetworkDataObjectCopy() {
		ReferenceTypeTestEntity testEntityA1 = new ReferenceTypeTestEntity();
		ReferenceTypeTestEntity testEntityA2 = new ReferenceTypeTestEntity();

		ClientA.Spawn(testEntityA1);
		CustomNetworkDataObject testNetworkDataObject = new CustomNetworkDataObject { Test = 10 };
		testEntityA1.Inventory.Add(testNetworkDataObject);
		testEntityA1.TestDerived = testNetworkDataObject with { };
		testEntityA2.Inventory.Add(testNetworkDataObject with { });
		testEntityA2.TestDerived = testNetworkDataObject with { };
		
		Cycle();

		Assert.IsTrue(ClientB.TryGetEntityByNetworkId(testEntityA1.NetworkId, out NetworkEntity entityB));
		ReferenceTypeTestEntity testEntityB = (ReferenceTypeTestEntity)entityB;

		Assert.AreEqual(10, testEntityB.TestDerived.Test);
		Assert.AreEqual(1, testEntityB.Inventory.Count);
		Assert.AreEqual(10, testEntityB.Inventory[0].Test);
	}
	
	
	[Test]
	public void Test_NetworkDataObjectCopy2() {

		ReferenceTypeTestEntity testEntity1 = new ReferenceTypeTestEntity();
		ReferenceTypeTestEntity testEntity2 = new ReferenceTypeTestEntity();
		
		testEntity1.Test = new CustomNetworkDataObject { Test = 10 };
		testEntity2.Test = testEntity1.Test with { };
		
		INetworkDataObject t1 = testEntity1.Test;
		INetworkDataObject t2 = testEntity2.Test;

		Assert.AreNotSame(t1.NetworkProperties, t2.NetworkProperties);
		Assert.AreNotSame(t1.Anchor, t2.Anchor);
		Assert.AreNotSame(t1.Parent, t2.Parent);
		Assert.AreNotSame(t1.PropertyIndex, t2.PropertyIndex);
		
	}


	[Test]
	public void Test_NetworkDataObjectRpcSerialization() {
		ReferenceTypeTestEntity testEntityA = new ReferenceTypeTestEntity();

		ClientA.Spawn(testEntityA);

		Cycle();

		Assert.IsTrue(ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityB));
		ReferenceTypeTestEntity testEntityB = (ReferenceTypeTestEntity)entityB;

		testEntityA.ReceivedRpc += OnReceivedRpc;

		bool receivedRpc = false;

		testEntityB.ReferenceRpc(new CustomNetworkDataObject { Test = 15 });

		Cycle();
		
		Assert.IsTrue(receivedRpc);
		

		void OnReceivedRpc(CustomNetworkDataObject obj) {
			receivedRpc = true;
			Assert.IsNotNull(obj);
			Assert.AreEqual(15, obj.Test);
		}
	}
	
	
	
	[Test]
	public void Test_NetworkDataObjectNested() {
		ReferenceTypeTestEntity testEntityA = new ReferenceTypeTestEntity();

		ClientA.Spawn(testEntityA);
		CustomNetworkDataObject testNetworkDataObject = new CustomNetworkDataObject { Test = 10 };

		testEntityA.TestNested = new NestedNetworkDataObject();

		Cycle();

		Assert.IsTrue(ClientB.TryGetEntityByNetworkId(testEntityA.NetworkId, out NetworkEntity entityB));
		ReferenceTypeTestEntity testEntityB = (ReferenceTypeTestEntity)entityB;

		
		Assert.IsNotNull(testEntityB.TestNested);

		testEntityA.TestNested.NestedProperty = new CustomNetworkDataObject { Test = 15 };
		
		Cycle();
		
		Assert.IsNotNull(testEntityB.TestNested.NestedProperty);
		Assert.AreEqual(15, testEntityB.TestNested.NestedProperty.Test);

	}
	
}