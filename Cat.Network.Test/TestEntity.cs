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

        public void Increment()
        {
            InvokeRPC(TestRPC);
        }

    }
}
