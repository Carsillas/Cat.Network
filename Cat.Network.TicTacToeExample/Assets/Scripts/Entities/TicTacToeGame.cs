using Cat.Network;
using Cat.Network.Steam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class TicTacToeGame : NetworkEntity {

	private NetworkProperty<ulong> XPlayer { get; } = new NetworkProperty<ulong>();
	private NetworkProperty<ulong> OPlayer { get; } = new NetworkProperty<ulong>();
	
	private NetworkProperty<int> PlayerTurnIndex { get; } = new NetworkProperty<int>();

	private NetworkProperty<string> TopLeft { get; } = new NetworkProperty<string>();
	private NetworkProperty<string> TopMiddle { get; } = new NetworkProperty<string>();
	private NetworkProperty<string> TopRight { get; } = new NetworkProperty<string>();
	private NetworkProperty<string> CenterLeft { get; } = new NetworkProperty<string>();
	private NetworkProperty<string> CenterMiddle { get; } = new NetworkProperty<string>();
	private NetworkProperty<string> CenterRight { get; } = new NetworkProperty<string>();
	private NetworkProperty<string> BottomLeft { get; } = new NetworkProperty<string>();
	private NetworkProperty<string> BottomMiddle { get; } = new NetworkProperty<string>();
	private NetworkProperty<string> BottomRight { get; } = new NetworkProperty<string>();


	public NetworkProperty<string>[] BoxIndices;

	public TicTacToeGame() {
		BoxIndices = new NetworkProperty<string>[] {
			TopLeft,
			TopMiddle,
			TopRight,
			CenterLeft,
			CenterMiddle,
			CenterRight,
			BottomLeft,
			BottomMiddle,
			BottomRight
		};

		PlayerTurnIndex.Value = -1;
	}


	[RPC(RPCInvokeSite.Owner)]
	private void Move(int index) {

		if(PlayerTurnIndex.Value == -1) {
			return;
		}

		SteamProfileEntity steamProfile = (SteamProfileEntity)RPCContext.Invoker;

		string newValue = null;
		if (steamProfile.Id.Value == XPlayer.Value) {
			newValue = "X";
		}
		if (steamProfile.Id.Value == OPlayer.Value) {
			newValue = "O";
		}
		if(newValue == null) {
			return;
		}

		int turn = PlayerTurnIndex.Value % 2;

		bool turnMatches = false;
		turnMatches |= turn == 0 && newValue == "X";
		turnMatches |= turn == 1 && newValue == "O";

		if (!turnMatches) {
			return;
		}

		if(BoxIndices[index].Value != null) {
			return;
		}

		BoxIndices[index].Value = newValue;
		PlayerTurnIndex.Value++;
	}

	public void Click(int index) {
		InvokeRPC(Move, index);
	}

	public void SetPlayers(ulong xPlayer, ulong oPlayer) {
		XPlayer.Value = xPlayer;
		OPlayer.Value = oPlayer;
		PlayerTurnIndex.Value = 0;
	}

}