using System.Runtime.CompilerServices;

namespace Cat.Async
{
	public interface IAwaiter : INotifyCompletion
	{
		bool IsCompleted { get; }
		void GetResult();
	}
}
