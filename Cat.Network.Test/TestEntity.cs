using System;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network.Test
{
    public class TestEntity : NetworkEntity
    {
        public NetworkProperty<int> TestInt { get; } = new NetworkProperty<int>();



        [RPC(RPCInvokeSite.Owner)]
        private void TestRPC()
        {
            TestInt.Value++;
        }

        [RPC(RPCInvokeSite.Owner)]
        private void TestRPC(int a)
        {
            TestInt.Value += a;
        }

        public void Increment()
        {
            InvokeRPC(TestRPC);
        }
        public void Add(int a)
        {
            InvokeRPC(TestRPC, a);
        }

    }
}
