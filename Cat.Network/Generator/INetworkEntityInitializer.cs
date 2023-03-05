using Cat.Network.Properties;
using System;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network.Generator
{
    public interface INetworkEntityInitializer
    {
        void Initialize();

        NetworkProperty[] NetworkProperties { get; set; }




    }
}
