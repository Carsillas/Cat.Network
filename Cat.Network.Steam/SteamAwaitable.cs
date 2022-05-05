//using Cat.Async;
//using System;
//using System.Collections.Generic;
//using System.Text;
//using System.Threading.Tasks;

//namespace Cat.SteamTransport {

//	internal class SteamAwaitable<T> : IAwaitable<T>, IAwaiter<T> {
//		public bool IsCompleted => false;

//		private Task<T> Task { get; }

//		private bool ContinuationSet { get; set; } = false;
//		private Action Continuation { get; set; }

//		private object LockObject { get; } = new object();

//		public SteamAwaitable(Task<T> task) {
//			Task = task;
//			Task.ContinueWith(task => TrySchedule());
//		}

//		public IAwaiter<T> GetAwaiter() {
//			return this;
//		}

//		public T GetResult() {
//			return Task.Result;
//		}

//		public void OnCompleted(Action continuation) {
//			lock (LockObject) {
//				Continuation = continuation;
//				ContinuationSet = true;
//				TrySchedule();
//			}
//		}

//		private void TrySchedule() {
//			lock (LockObject) {
//				if (ContinuationSet && Task.IsCompleted) {
//					Instance.SteamResultContinuations.Enqueue(Continuation);
//				}
//			}
//		}

//	}
//}
