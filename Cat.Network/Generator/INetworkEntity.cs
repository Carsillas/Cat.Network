using Cat.Network.Properties;
using Cat.Network.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network.Generator
{
    public interface INetworkEntity
    {
        void Initialize();

        NetworkProperty[] NetworkProperties { get; set; }
		ISerializationContext SerializationContext { get; set; }
	}
}
