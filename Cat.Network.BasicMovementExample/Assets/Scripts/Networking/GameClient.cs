using Cat.Network;
using Cat.Network.Steam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class LocalGameClient : LocalSteamGameClient {

	public LocalGameClient(GameServer server, IProxyManager proxyManager) : base(server, proxyManager) {

		SerializationContext.RegisterSerializationFunction<Vector3>(Serializers.SerializeVector3, Serializers.DeserializeVector3);
		SerializationContext.RegisterSerializationFunction<Vector2>(Serializers.SerializeVector2, Serializers.DeserializeVector2);
	}

}

public class RemoteGameClient : RemoteSteamGameClient {

	public RemoteGameClient(ulong targetSteamId, IProxyManager proxyManager) : base(targetSteamId, proxyManager) {

		SerializationContext.RegisterSerializationFunction<Vector3>(Serializers.SerializeVector3, Serializers.DeserializeVector3);
		SerializationContext.RegisterSerializationFunction<Vector2>(Serializers.SerializeVector2, Serializers.DeserializeVector2);
	} 

}