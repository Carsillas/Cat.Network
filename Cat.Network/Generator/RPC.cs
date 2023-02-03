using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cat.Network.Generator
{

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Delegate)]
    public class RPCAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class MulticastAttribute : Attribute
    {

        public bool ExecuteOnServer { get; set; }

    }

}
