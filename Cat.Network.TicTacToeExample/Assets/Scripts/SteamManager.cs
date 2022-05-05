using Cat.Network.Steam;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class SteamManager : MonoBehaviour {

	private Steam Steam { get; set; }

	private SteamGameServer SteamGameServer { get; set; }
	private LocalSteamGameClient SteamGameClient { get; set; }


	private IEnumerator Start() {
		Redirect();

		Steam = new Steam();

		Steam.OnLobbyChanged += Steam_OnLobbyChanged;
		Steam.OnLobbyCreated += Steam_OnLobbyCreated;
		Steam.OnLobbyGameServerSet += Steam_OnLobbyGameServerSet;

		yield return new WaitForSeconds(1.0f);
		Steam.CreateLobby(2);

	}

	private void Steam_OnLobbyCreated() {
		SteamGameServer = new SteamGameServer(new EntityStorage());
	}

	private void Steam_OnLobbyGameServerSet() {
		SteamGameClient = new LocalSteamGameClient(SteamGameServer);
		SteamGameClient.ProxyManager = new ProxyManager();
	}

	private void Steam_OnLobbyChanged() {

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






	private class UnityTextWriter : TextWriter {
		private StringBuilder buffer = new StringBuilder();

		public override void Flush() {
			Debug.Log(buffer.ToString());
			buffer.Length = 0;
		}

		public override void Write(string value) {
			buffer.Append(value);
			if (value != null) {
				var len = value.Length;
				if (len > 0) {
					var lastChar = value[len - 1];
					if (lastChar == '\n') {
						Flush();
					}
				}
			}
		}

		public override void Write(char value) {
			buffer.Append(value);
			if (value == '\n') {
				Flush();
			}
		}

		public override void Write(char[] value, int index, int count) {
			Write(new string(value, index, count));
		}

		public override Encoding Encoding {
			get { return Encoding.Default; }
		}
	}

	public static void Redirect() {
		Console.SetOut(new UnityTextWriter());
	}

}
