using Cat.Network.Steam;
using Steamworks;
using Steamworks.Data;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class SteamManager : MonoBehaviour {

	public Steam Steam { get; private set; }

	private GameServer SteamGameServer { get; set; }
	private SteamGameClient SteamGameClient { get; set; }


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

		SteamGameServer = new GameServer(new EntityStorage());
	}

	private void Steam_OnLobbyGameServerSet(ulong targetSteamId) {

		ProxyManager proxyManager = new ProxyManager();

		if (targetSteamId == SteamClient.SteamId) {
			SteamGameClient = new LocalGameClient(SteamGameServer, proxyManager);
		} else {
			SteamGameClient = new RemoteGameClient(targetSteamId, proxyManager);
		}

		proxyManager.Client = SteamGameClient;
	}

	private void Steam_OnLobbyChanged(Lobby? lobby) {
		uint a = 0;
		ushort b = 0;
		SteamId targetSteamId = default;
		if (lobby.HasValue && lobby.Value.GetGameServer(ref a, ref b, ref targetSteamId)) {
			if (targetSteamId != SteamClient.SteamId) {
				ProxyManager proxyManager = new ProxyManager();
				SteamGameClient = new RemoteGameClient(targetSteamId, new ProxyManager());
				proxyManager.Client = SteamGameClient;
			}
		}
	}

	private void Update() {
		Steam.Tick();
		SteamGameServer?.Tick();
		SteamGameClient?.Tick();
	}

	private void OnApplicationQuit() {
		Steam.Dispose();
	}

}
