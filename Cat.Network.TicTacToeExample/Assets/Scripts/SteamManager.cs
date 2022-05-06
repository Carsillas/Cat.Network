using Cat.Network.Steam;
using Steamworks;
using Steamworks.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class SteamManager : MonoBehaviour {

	public Steam Steam { get; private set; }

	private SteamGameServer SteamGameServer { get; set; }
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

		SteamGameServer = new SteamGameServer(new EntityStorage());
	}

	private void Steam_OnLobbyGameServerSet(ulong targetSteamId) {

		if (targetSteamId == SteamClient.SteamId) {
			SteamGameClient = new LocalSteamGameClient(SteamGameServer, new ProxyManager());
			SteamGameClient.Spawn(new TicTacToeGame());
		} else {
			SteamGameClient = new RemoteSteamGameClient(targetSteamId, new ProxyManager());
		}
	}

	private void Steam_OnLobbyChanged(Lobby? lobby) {
		uint a = 0;
		ushort b = 0;
		SteamId targetSteamId = default;
		if (lobby.HasValue && lobby.Value.GetGameServer(ref a, ref b, ref targetSteamId)) {
			if (targetSteamId != SteamClient.SteamId) {
				SteamGameClient = new RemoteSteamGameClient(targetSteamId, new ProxyManager());
			}
		}
	}

	// Update is called once per frame
	private void Update() {
		Steam.Tick();
		SteamGameServer?.Tick();
		SteamGameClient?.Tick();
	}

	private void OnApplicationQuit() {
		Steam.Dispose();
	}

}
