using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BulletBehavior : EntityBehavior<Bullet> {

	void Start() {
		transform.position = Entity.Position.Value;
	}

	void Update() {
		transform.position += Entity.Velocity.Value * Time.deltaTime;
	}
}
