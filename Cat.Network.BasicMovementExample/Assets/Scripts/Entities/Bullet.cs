using Cat.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class Bullet : NetworkEntity {

	public NetworkProperty<Vector3> Position { get; } = new NetworkProperty<Vector3>();
	public NetworkProperty<Vector3> Velocity { get; } = new NetworkProperty<Vector3>();

}