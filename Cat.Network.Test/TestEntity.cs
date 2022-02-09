using System;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network.Test
{
    public class TestEntity : NetworkEntity
    {
        public NetworkProperty<int> TestInt { get; } = new NetworkProperty<int>();

        public RAC<int, int> PrintSum { get; } = new RAC<int, int>(RPCInvokeSite.Owner);


    }
}
