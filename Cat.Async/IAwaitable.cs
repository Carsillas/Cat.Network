using System;
using System.Threading.Tasks;

namespace Cat.Async
{
    public interface IAwaitable
    {
        IAwaiter GetAwaiter();

    }
    public interface IAwaitable<T>
    {
        IAwaiter<T> GetAwaiter();
    }

}
