using Cat.Network.Steam;
using Steamworks;
using Steamworks.Data;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class SteamManager : MonoBehaviour {

	public Steam Steam { get; private set; }

	private GameServer GameServer { get; set; }
	private GameClient GameClient { get; set; }


	private void Start() {

		Steam = new Steam();

		Steam.OnLobbyChanged += Steam_OnLobbyChanged;
		Steam.OnLobbyCreated += Steam_OnLobbyCreated;
		Steam.OnLobbyGameServerSet += Steam_OnLobbyGameServerSet;
	}

	private void Steam_OnLobbyCreated(Lobby lobby) {

		TextEditor textEditor = new TextEditor();
		textEditor.text = lobby.Id.ToString();
		textEditor.SelectAll();
		textEditor.Copy();

		GameServer = new GameServer(new EntityStorage());
	}

	private void Steam_OnLobbyGameServerSet(ulong targetSteamId) {

		ProxyManager proxyManager = new ProxyManager();

		if (targetSteamId == SteamClient.SteamId) {
			GameClient = new GameClient(GameServer, proxyManager);
		} else {
			GameClient = new GameClient(targetSteamId, proxyManager);
		}

		proxyManager.Client = GameClient;
	}

	private void Steam_OnLobbyChanged(Lobby? lobby) {
		uint a = 0;
		ushort b = 0;
		SteamId targetSteamId = default;
		if (lobby.HasValue && lobby.Value.GetGameServer(ref a, ref b, ref targetSteamId)) {
			if (targetSteamId != SteamClient.SteamId) {
				ProxyManager proxyManager = new ProxyManager();
				GameClient = new GameClient(targetSteamId, proxyManager);
				proxyManager.Client = GameClient;
			}
		}
	}

	private void Update() {
		Steam.Tick();
		GameServer?.Tick();
		GameClient?.Tick();
	}

	private void OnApplicationQuit() {
		Steam.Dispose();
	}

}
