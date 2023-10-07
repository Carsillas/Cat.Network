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
		private object LockObject { get; } = new();


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

	internal class SteamAwaitable<T> : SteamAwaitable, IAwaitable<T>, IAwaiter<T> {

		private Task<T> Task { get; }

		public SteamAwaitable(Steam steam, Task<T> task) : base(steam, task) {
			Task = task;
		}

		IAwaiter<T> IAwaitable<T>.GetAwaiter() {
			return this;
		}

		T IAwaiter<T>.GetResult() {
			return Task.Result;
		}
	}

}
