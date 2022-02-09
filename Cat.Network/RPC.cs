using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cat.Network
{
    public enum RPCInvokeSite
    {
        Owner,
        Server,
        All
    }

    public abstract class RPC
    {
        internal RPCInvokeSite InvokeSite { get; }
        internal NetworkEntity Entity { get; set; }
        internal string ID { get; set; }

        public RPC(RPCInvokeSite invokeSite)
        {
            InvokeSite = invokeSite;
        }

        internal abstract void HandleIncomingInvocation(BinaryReader reader);

    }

}
