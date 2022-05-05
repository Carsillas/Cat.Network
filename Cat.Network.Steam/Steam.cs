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

		public event Action OnLobbyChanged;
		public event Action OnLobbyCreated;
		public event Action OnLobbyGameServerSet;

		private Lobby? Lobby { get; set; }
		private ConcurrentQueue<Action> SteamResultContinuations { get; } = new ConcurrentQueue<Action>();

		public Steam() {
			Init();

			SteamMatchmaking.OnLobbyEntered += SteamMatchmaking_OnLobbyEntered;
			SteamMatchmaking.OnLobbyGameCreated += SteamMatchmaking_OnLobbyGameCreated;
			SteamMatchmaking.OnLobbyCreated += SteamMatchmaking_OnLobbyCreated;
		}

		private void SteamMatchmaking_OnLobbyCreated(Result result, Lobby lobby) {
			if (result == Result.OK) {
				OnLobbyCreated?.Invoke();
				lobby.SetGameServer(lobby.Owner.Id);
			}
		}

		private void SteamMatchmaking_OnLobbyGameCreated(Lobby lobby, uint ip, ushort port, SteamId targetSteamId) {
			OnLobbyGameServerSet?.Invoke();
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
		}

		private void SteamMatchmaking_OnLobbyEntered(Lobby lobby) {
			Lobby = lobby;

			OnLobbyChanged?.Invoke();
		}

		public void Dispose() {
			FacepunchClient.Shutdown();
		}
	}
}
