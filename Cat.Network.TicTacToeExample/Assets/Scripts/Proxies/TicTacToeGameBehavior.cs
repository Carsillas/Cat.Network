using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class TicTacToeGameBehavior : EntityBehavior<TicTacToeGame> {

	[SerializeField]
	private Material BlankMat;
	[SerializeField]
	private Material XMat;
	[SerializeField]
	private Material OMat;

	[SerializeField]
	private int Index;


	private void Start() {
		Entity.BoxIndices[Index].OnValueChanged += State_OnValueChanged;
		UpdateMaterial();
	}

	private void State_OnValueChanged(string Previous) {
		UpdateMaterial();
	}

	private void UpdateMaterial() {
		switch (Entity.BoxIndices[Index].Value) {
			case "X":
				GetComponent<Renderer>().material = XMat;
				break;
			case "O":
				GetComponent<Renderer>().material = OMat;
				break;
			default:
				GetComponent<Renderer>().material = BlankMat;
				break;
		}
	}

	private void OnMouseUpAsButton() {
		Entity.Click(Index);
	}




}