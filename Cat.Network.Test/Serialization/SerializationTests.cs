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


		TestRPC(testEntityB,
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
		Server.Tick();
		ClientB.Tick();

		Assert.IsTrue(testEntityA.RPCInvoked);
		Assert.IsFalse(testEntityB.RPCInvoked);

	}

	private void TestRPC(SerializationTestEntity testEntityB, System.Boolean BooleanParam, System.Byte ByteParam, System.Int16 ShortParam, System.Int32 IntParam, System.Int64 LongParam, System.UInt16 UShortParam, System.UInt32 UIntParam, System.UInt64 ULongParam, System.String StringParam) {
		

		var serializationContext = ((Cat.Network.INetworkEntity)testEntityB).SerializationContext;
		System.Span<byte> buffer = serializationContext.RentRPCBuffer(testEntityB);
		System.Span<byte> bufferCopy = buffer.Slice(4);
		

		int lengthStorage;
		System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(bufferCopy, 8713199717368105526L); bufferCopy = bufferCopy.Slice(8);
		System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bufferCopy, 1); bufferCopy.Slice(4)[0] = BooleanParam ? (byte)1 : (byte)0; bufferCopy = bufferCopy.Slice(4 + 1);
		System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bufferCopy, 1); bufferCopy.Slice(4)[0] = ByteParam; bufferCopy = bufferCopy.Slice(4 + 1);
		System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bufferCopy, 2); System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(bufferCopy.Slice(4), ShortParam); bufferCopy = bufferCopy.Slice(4 + 2);
		System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bufferCopy, 4); System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bufferCopy.Slice(4), IntParam); bufferCopy = bufferCopy.Slice(4 + 4);
		System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bufferCopy, 8); System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(bufferCopy.Slice(4), LongParam); bufferCopy = bufferCopy.Slice(4 + 8);
		System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bufferCopy, 2); System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bufferCopy.Slice(4), UShortParam); bufferCopy = bufferCopy.Slice(4 + 2);
		System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bufferCopy, 4); System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bufferCopy.Slice(4), UIntParam); bufferCopy = bufferCopy.Slice(4 + 4);
		System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bufferCopy, 8); System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(bufferCopy.Slice(4), ULongParam); bufferCopy = bufferCopy.Slice(4 + 8);
		lengthStorage = System.Text.Encoding.Unicode.GetBytes(StringParam, bufferCopy.Slice(4)); System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bufferCopy, lengthStorage); bufferCopy = bufferCopy.Slice(4 + lengthStorage);
		System.Int32 contentLength = buffer.Length - bufferCopy.Length;
		System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(buffer, contentLength - 4);

	}



}
