using Cat.Network;
using Cat.Network.Steam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class GameClient : SteamGameClient {

	public GameClient(GameServer server, IProxyManager proxyManager) : base(server, proxyManager) {
		AddSerializers();
	}

	public GameClient(ulong targetSteamId, IProxyManager proxyManager) : base(targetSteamId, proxyManager) {
		AddSerializers();
	}

	private void AddSerializers() {
		SerializationContext.RegisterSerializationFunction<Vector3>(Serializers.SerializeVector3, Serializers.DeserializeVector3);
		SerializationContext.RegisterSerializationFunction<Vector2>(Serializers.SerializeVector2, Serializers.DeserializeVector2);
	}
}