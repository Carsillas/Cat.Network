using Cat.Network.Steam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class PlayerBehavior : EntityBehavior<SteamProfileEntity> {

	private static List<PlayerBehavior> Players { get; } = new List<PlayerBehavior>();

	public static event Action<ulong, ulong> OnPlayersReady;

	private void Start() {
		Players.Add(this);

		if(Players.Count == 2) {
			OnPlayersReady?.Invoke(Players[0].Entity.Id.Value, Players[1].Entity.Id.Value);
		}

	}

	private void OnDestroy() {
		Players.Remove(this);
	}

}
