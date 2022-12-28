using System;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network.Test {


	public class TestProfileEntity : NetworkEntity {

	}

	public class TestEntity : NetworkEntity {
		public NetworkProperty<int> TestInt { get; } = new NetworkProperty<int>();
		public NetworkProperty<int> TestIntCreateOnly { get; } = new NetworkProperty<int>(NetworkPropertySerializeTrigger.Creation);

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
