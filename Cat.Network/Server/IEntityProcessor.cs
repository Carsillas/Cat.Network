using Cat.Network.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Server;
public interface IEntityProcessor
{
    void CreateEntity(NetworkEntity entity);
    void UpdateEntity(NetworkEntity entity);
    void DeleteEntity(NetworkEntity entity);
    void NotifyAssignedOwner(NetworkEntity entity);


    IReadOnlySet<NetworkEntity> RelevantEntities { get; }

}

