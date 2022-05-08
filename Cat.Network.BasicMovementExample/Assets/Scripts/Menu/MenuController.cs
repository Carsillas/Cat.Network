using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MenuController : MonoBehaviour {

	[SerializeField]
	private InputField LobbyInputField;

	private SteamManager SteamManager { get; set; }

	private void Start() {
		SteamManager = FindObjectOfType<SteamManager>();
	}

	public void CreateLobby() {
		SteamManager.Steam.CreateLobby(2);
		gameObject.SetActive(false);
	}

	public void JoinLobby() {
		if (ulong.TryParse(LobbyInputField.text, out ulong lobbyId)) {
			SteamManager.Steam.JoinLobby(lobbyId);
			gameObject.SetActive(false);
		}
	}

}
