using Steamworks;
using Steamworks.Data;
using System;
using System.Threading.Tasks;

using FacepunchClient = Steamworks.SteamClient;

namespace Cat.Network.Steam {
	public class SteamClient {

		private Lobby? Lobby { get; set; }

		public Task Initialized { get; private set; }

		public SteamClient() {
			Init();

			SteamMatchmaking.OnLobbyEntered += SteamMatchmaking_OnLobbyEntered;
			SteamMatchmaking.OnLobbyDataChanged += SteamMatchmaking_OnLobbyDataChanged;
		}

		private void SteamMatchmaking_OnLobbyDataChanged(Lobby lobby) {
			Console.WriteLine("Lobby data changed");
		}

		private void Init() {
			TaskCompletionSource<bool> completionSource = new TaskCompletionSource<bool>();
			FacepunchClient.Init(480, false);

			Initialized = completionSource.Task;

			Task.Run(async () => {
				while (!FacepunchClient.IsValid) {
					await Task.Delay(10);
				}

				completionSource.SetResult(true);
			});
		}

		private void SteamMatchmaking_OnLobbyEntered(Lobby lobby) {
			Lobby = lobby;
			Console.WriteLine($"Lobby id: {lobby.Id}");
		}

		public void Tick() {
			FacepunchClient.RunCallbacks();
		}

		public async Task CreateLobby(int maxPlayers) {
			Lobby? lobby = await SteamMatchmaking.CreateLobbyAsync(maxPlayers);
			Console.WriteLine($"lobby id: {lobby.Value.Id}");
		}

		public async Task JoinLobby(ulong id) {
			Lobby? lobby = await SteamMatchmaking.JoinLobbyAsync(id);
		}

	}
}
