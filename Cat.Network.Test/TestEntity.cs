using System;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network.Test {
	public class TestEntity : NetworkEntity {
		public NetworkProperty<int> TestInt { get; } = new NetworkProperty<int>();

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

		public void Increment() {
			InvokeRPC(TestRPC);
		}
		public void Add(int a) {
			InvokeRPC(TestRPC, a);
		}

		public void InvokeTestMulticast() {
			Multicast(TestMulticast);
		}

	}
}
