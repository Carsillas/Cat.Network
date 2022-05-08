using Cat.Network;
using UnityEngine;

public abstract class EntityBehavior : MonoBehaviour {

	public Client Client { get; set; }

}

public abstract class EntityBehavior<T> : EntityBehavior where T : NetworkEntity {

	public T Entity { get; set; }

}

public interface IOwnerEntityBehavior { }
