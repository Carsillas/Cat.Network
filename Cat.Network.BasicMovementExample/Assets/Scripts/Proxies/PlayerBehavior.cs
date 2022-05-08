using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class PlayerBehavior : EntityBehavior<Player> {

	[SerializeField]
	private CharacterController Controller;

	private void Start() {
		transform.position = Entity.Position.Value;
		transform.rotation = Quaternion.Euler(0, Entity.Yaw.Value, 0);
	}

	private void Update() {
		transform.position = Entity.Position.Value;
		transform.rotation = Quaternion.Euler(0, Entity.Yaw.Value, 0);
		Vector3 movementInput = new Vector3(Entity.MovementInput.Value.x, 0, Entity.MovementInput.Value.y) * 5.0f;

		Controller.SimpleMove(transform.rotation * movementInput);
	}

}