using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class TicTacToeOwnerBehavior : EntityBehavior<TicTacToeGame>, IOwnerEntityBehavior {

	public static TicTacToeOwnerBehavior Instance { get; private set; }

	private void Start() {
		Instance = this;
		ProfileBehavior.OnPlayersReady += SetPlayers;
	}

	private void OnDestroy() {
		Instance = null;
	}

	public void SetPlayers(ulong xPlayer, ulong oPlayer) {

		Entity.SetPlayers(xPlayer, oPlayer);
	}

}
