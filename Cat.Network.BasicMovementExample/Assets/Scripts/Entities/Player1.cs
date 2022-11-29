using Cat.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class Player : NetworkEntity {

	public NetworkProperty<float> Yaw { get; } = new NetworkProperty<float>();

	public NetworkProperty<Vector3> Position { get; } = new NetworkProperty<Vector3>();

	public Player() {
		DestroyWithOwner.Value = true;

	}

}
