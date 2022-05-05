using Cat.Network;
using Cat.Network.Steam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class ProxyManager : IProxyManager {
	public void OnEntityCreated(NetworkEntity entity) {
		if(entity is SteamProfileEntity steamProfile) {
			Debug.Log(steamProfile.Name.Value);
		}
	}

	public void OnEntityDeleted(NetworkEntity entity) {

	}
}
