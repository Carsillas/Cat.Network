using Steamworks;
using Steamworks.Data;
using System;
using System.Threading;
using System.Threading.Tasks;

using FacepunchClient = Steamworks.SteamClient;

namespace Cat.Network.Steam {
	public class Steam {

		public Task Initialized { get; private set; }

		public event Action OnLobbyChanged;
		public event Action OnLobbyGameServerSet;

		private Lobby? Lobby { get; set; }
		private SemaphoreSlim SteamApiAccess { get; } = new SemaphoreSlim(1, 1);

		public Steam() {
			Init();

			SteamMatchmaking.OnLobbyEntered += SteamMatchmaking_OnLobbyEntered;
			SteamMatchmaking.OnLobbyGameCreated += SteamMatchmaking_OnLobbyGameCreated;
		}

		private void SteamMatchmaking_OnLobbyGameCreated(Lobby lobby, uint ip, ushort port, SteamId targetSteamId) {
			OnLobbyGameServerSet?.Invoke();
		}

		public void Tick() {
			FacepunchClient.RunCallbacks();
		}

		public async Task CreateLobby(int maxPlayers) {
			try {
				await SteamApiAccess.WaitAsync();
				await SteamMatchmaking.CreateLobbyAsync(maxPlayers);
			} finally {
				SteamApiAccess.Release();
			}
		}

		public async Task JoinLobby(ulong id) {
			try {
				await SteamApiAccess.WaitAsync();
				await SteamMatchmaking.JoinLobbyAsync(id);
			} finally {
				SteamApiAccess.Release();
			}
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
			OnLobbyChanged?.Invoke();
			if (lobby.Owner.IsMe) {
				lobby.SetGameServer(lobby.Owner.Id);
			}
		}

	}
}
