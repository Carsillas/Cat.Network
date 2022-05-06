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


		private ConcurrentQueue<Action> SteamResultContinuations { get; } = new ConcurrentQueue<Action>();

		public Steam() {
			Init();

			SteamMatchmaking.OnLobbyEntered += SteamMatchmaking_OnLobbyEntered;
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

		public IAwaitable CreateLobby(int maxPlayers) {
			return new SteamAwaitable(this, SteamMatchmaking.CreateLobbyAsync(maxPlayers));
		}

		public IAwaitable JoinLobby(ulong id) {
			return new SteamAwaitable(this, SteamMatchmaking.JoinLobbyAsync(id));

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

		private void SteamMatchmaking_OnLobbyEntered(Lobby lobby) {
			OnLobbyChanged?.Invoke(lobby);
		}

		public void Dispose() {
			FacepunchClient.Shutdown();
		}
	}
}
