using Cat.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public abstract class EntityBehavior : MonoBehaviour {

}

public abstract class EntityBehavior<T> : EntityBehavior where T : NetworkEntity {

	[SerializeField]
	public T Entity;

}

public interface IOwnerEntityBehavior { }
