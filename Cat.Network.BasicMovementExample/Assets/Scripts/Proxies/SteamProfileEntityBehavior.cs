using Cat.Network.Steam;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SteamProfileEntityBehavior : EntityBehavior<SteamProfileEntity>, IOwnerEntityBehavior {
	private void Start() {

		Client.Spawn(new Player());

	}
}
