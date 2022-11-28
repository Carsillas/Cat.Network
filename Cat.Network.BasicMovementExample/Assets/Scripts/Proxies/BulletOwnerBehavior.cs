using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletOwnerBehavior : EntityBehavior<Bullet>, IOwnerEntityBehavior {

	private IEnumerator Start() {
		yield return new WaitForSeconds(5.0f);
		Client.Despawn(Entity);
	}

}
