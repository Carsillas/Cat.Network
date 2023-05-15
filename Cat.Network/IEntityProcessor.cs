using System.Collections.Generic;

namespace Cat.Network;
public interface IEntityProcessor
{
    void CreateEntity(NetworkEntity entity, bool isOwner);
    void UpdateEntity(NetworkEntity entity, bool isOwner);
    void DeleteEntity(NetworkEntity entity);
    void NotifyAssignedOwner(NetworkEntity entity);


    IReadOnlySet<NetworkEntity> RelevantEntities { get; }

}

