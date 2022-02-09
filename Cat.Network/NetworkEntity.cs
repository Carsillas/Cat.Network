using System;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network
{
    public abstract class NetworkEntity
    {
        public Guid NetworkID { get; internal set; }

        internal NetworkEntitySerializer Serializer { get; }

        public NetworkEntity()
        {
            Serializer = new NetworkEntitySerializer(this);
        }

    }
}
