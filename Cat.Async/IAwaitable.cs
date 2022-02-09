using System;
using System.Threading.Tasks;

namespace Cat.Async
{
    public interface IAwaitable
    {
        IAwaiter GetAwaiter();

    }
}
