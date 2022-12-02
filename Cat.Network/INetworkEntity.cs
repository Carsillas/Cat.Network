using System;

namespace Cat.Network
{
    public interface INetworkEntity
    {
        Guid NetworkID { get; }
        NetworkProperty<bool> DestroyWithOwner { get; }
        bool IsOwner { get; }
    }
}