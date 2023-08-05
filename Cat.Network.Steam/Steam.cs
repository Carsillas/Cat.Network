using Cat.Async;
using Steamworks;
using Steamworks.Data;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using FacepunchClient = Steamworks.SteamClient;

namespace Cat.Network.Steam {
	public class Steam : IDisposable {

		public event Action<Lobby?> OnLobbyChanged;
		public event Action<Lobby> OnLobbyCreated;
		public event Action<ulong> OnLobbyGameServerSet;

		private Lobby? CurrentLobby { get; set; }

		private ConcurrentQueue<Action> SteamResultContinuations { get; } = new();

		public Steam() {
			Init();
			SteamMatchmaking.OnLobbyGameCreated += SteamMatchmaking_OnLobbyGameCreated;
			SteamMatchmaking.OnLobbyCreated += SteamMatchmaking_OnLobbyCreated;
		}

		private void SteamMatchmaking_OnLobbyCreated(Result result, Lobby lobby) {
			if (result == Result.OK) {
				OnLobbyCreated?.Invoke(lobby);
				lobby.SetGameServer(FacepunchClient.SteamId);
				lobby.SetPublic();
				lobby.SetJoinable(true);
			}
		}

		private void SteamMatchmaking_OnLobbyGameCreated(Lobby lobby, uint ip, ushort port, SteamId targetSteamId) {
			OnLobbyGameServerSet?.Invoke(targetSteamId.Value);
		}

		public void Tick() {
			FacepunchClient.RunCallbacks();
			while (SteamResultContinuations.TryDequeue(out Action continuation)) {
				continuation?.Invoke();
			}
		}

		public IAwaitable<Lobby?> CreateLobby(int maxPlayers) {
			return new SteamAwaitable<Lobby?>(this, SteamMatchmaking.CreateLobbyAsync(maxPlayers));
		}

		public IAwaitable<Lobby?> JoinLobby(ulong id) {

			Task<Lobby?> task = Task.Run(async () => {
				Lobby lobby = new Lobby(id);
				var result = await lobby.Join();

				if(result == RoomEnter.Success) {
					CurrentLobby = lobby;
					OnLobbyChanged?.Invoke(lobby);
					return (Lobby?) lobby;
				} else {
					return null;
				}
			});

			return new SteamAwaitable<Lobby?>(this, task);

		}

		public void LeaveLobby() {
			CurrentLobby?.Leave();
			CurrentLobby = null;
			OnLobbyChanged?.Invoke(null);
		}

		internal void QueueSteamTaskContinuation(Action continuation) {
			SteamResultContinuations.Enqueue(continuation);
		}

		private void Init() {
			FacepunchClient.Init(480, false);
			Task.Run(async () => {
				while (!FacepunchClient.IsValid) {
					await Task.Delay(10);
				}
				SteamNetworkingUtils.InitRelayNetworkAccess();
			});
		}

		public void Dispose() {
			FacepunchClient.Shutdown();
		}
	}
}
