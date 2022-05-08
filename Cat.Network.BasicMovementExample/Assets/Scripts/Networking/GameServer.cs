using Cat.Network;
using Cat.Network.Steam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class GameServer : SteamGameServer {

	public GameServer(IEntityStorage entityStorage) : base(entityStorage) {

		SerializationContext.RegisterSerializationFunction<Vector3>(Serializers.SerializeVector3, Serializers.DeserializeVector3);
		SerializationContext.RegisterSerializationFunction<Vector2>(Serializers.SerializeVector2, Serializers.DeserializeVector2);

		Spawn(new Terrain());

	}


}
