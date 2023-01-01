using System;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network.Test {


	public class TestProfileEntity : NetworkEntity {

	}

	public enum Test : byte {
		Meow,
		Woof,
		Purple
	}

	public class TestCompoundProperty {

		public NetworkProperty<int> A { get; } = new NetworkProperty<int>();
		public NetworkProperty<int> B { get; } = new NetworkProperty<int>();

	}

	public class TestEntity : NetworkEntity {
		public NetworkProperty<int> TestInt { get; } = new NetworkProperty<int>();
		public NetworkProperty<Test> TestEnum { get; } = new NetworkProperty<Test>();
		public NetworkProperty<int> TestIntCreateOnly { get; } = new NetworkProperty<int>(NetworkPropertySerializeTrigger.Creation);
		public NetworkProperty<TestEntity> TestEntityReference { get; } = new NetworkProperty<TestEntity>();

		public CompoundNetworkProperty<TestCompoundProperty> TestCompound { get; } = new CompoundNetworkProperty<TestCompoundProperty>();


		public bool MulticastExecuted { get; set; }


		[RPC]
		private void TestRPC() {
			TestInt.Value++;
		}

		[RPC]
		private void TestRPC(int a) {
			TestInt.Value += a;
		}


		[Multicast]
		private void TestMulticast() {
			MulticastExecuted = true;
		}

		[Multicast(ExecuteOnServer = true)]
		private void IncrementTestIntCreateOnly() {
			TestIntCreateOnly.Value++;
		}


		public void Increment() {
			InvokeRPC(TestRPC);
		}
		public void Add(int a) {
			InvokeRPC(TestRPC, a);
		}

		public void InvokeTestMulticast() {
			Multicast(TestMulticast);
		}

		public void InvokeIncrementTestIntCreateOnly() {
			Multicast(IncrementTestIntCreateOnly);
		}

	}
}
