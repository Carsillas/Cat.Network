using Cat.Async;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Steam {

	internal class SteamAwaitable : IAwaitable, IAwaiter {
		public bool IsCompleted => false;

		private Steam Steam { get; }
		private Task Task { get; }


		private bool ContinuationSet { get; set; } = false;
		private Action Continuation { get; set; }
		private object LockObject { get; } = new object();


		public SteamAwaitable(Steam steam, Task task) {
			Steam = steam;
			Task = task;
			Task.ContinueWith(t => TrySchedule());
		}

		public IAwaiter GetAwaiter() {
			return this;
		}

		public void GetResult() { }

		public void OnCompleted(Action continuation) {
			lock (LockObject) {
				Continuation = continuation;
				ContinuationSet = true;
				TrySchedule();
			}
		}

		private void TrySchedule() {
			lock (LockObject) {
				if (ContinuationSet && Task.IsCompleted) {
					Steam.QueueSteamTaskContinuation(Continuation);
				}
			}
		}

	}
}
