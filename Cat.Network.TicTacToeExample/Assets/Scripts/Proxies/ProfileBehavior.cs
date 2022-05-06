using Cat.Network.Steam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class ProfileBehavior : EntityBehavior<SteamProfileEntity> {

	private static List<ProfileBehavior> Profiles { get; } = new List<ProfileBehavior>();

	public static event Action<ulong, ulong> OnPlayersReady;

	private void Start() {
		Profiles.Add(this);

		if(Profiles.Count == 2) {
			OnPlayersReady?.Invoke(Profiles[0].Entity.Id.Value, Profiles[1].Entity.Id.Value);
		}

	}

	private void OnDestroy() {
		Profiles.Remove(this);
	}

}
