
using BenchmarkDotNet.Attributes;
using Cat.Network.Test.Serialization;

namespace Cat.Network.Test;


[MemoryDiagnoser]
public class MemoryTest {
	public static CatNetworkTest Test { get; set; }
	public static SerializationTestEntity TestEntity { get; set; }


	[GlobalSetup]
	public void Setup() {
		Test = new CatNetworkTest();
		Test.Setup();

		const string WowString = "Wow!";

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

		Test.ClientA.Spawn(testEntityA);

		Test.ClientA.Tick();
		Test.Server.Tick();
		Test.ClientB.Tick();

		Test.ClientB.TryGetEntityByNetworkID(testEntityA.NetworkID, out NetworkEntity entityB);

		TestEntity = (SerializationTestEntity)entityB;
	}


	[Benchmark]
	[IterationCount(250)]
	public void ExecuteRPCAndTick() {

		TestEntity.TestMemoryRPC(
			TestEntity.BooleanProperty,
			TestEntity.ByteProperty,
			TestEntity.ShortProperty,
			TestEntity.IntProperty,
			TestEntity.LongProperty,
			TestEntity.UShortProperty,
			TestEntity.UIntProperty,
			TestEntity.ULongProperty);

		Test.ClientB.Tick();
		Test.Server.Tick();
		Test.ClientA.Tick();
		Test.Server.Tick();
		Test.ClientB.Tick();

	}
}