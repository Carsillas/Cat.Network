using System.Runtime.CompilerServices;

namespace Cat.Async {
	public interface IAwaiter : INotifyCompletion {
		bool IsCompleted { get; }
		void GetResult();
	}
	public interface IAwaiter<T> : INotifyCompletion {
		bool IsCompleted { get; }
		T GetResult();
	}

}
