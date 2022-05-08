using Cat.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public static class Serializers {

	public static void SerializeVector3(BinaryWriter writer, Vector3 value) {
		writer.Write(value.x);
		writer.Write(value.y);
		writer.Write(value.z);
	}
	public static Vector3 DeserializeVector3(BinaryReader reader, NetworkProperty<Vector3> NetworkProperty) {
		return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
	}

	public static void SerializeVector2(BinaryWriter writer, Vector2 value) {
		writer.Write(value.x);
		writer.Write(value.y);
	}
	public static Vector2 DeserializeVector2(BinaryReader reader, NetworkProperty<Vector2> NetworkProperty) {
		return new Vector2(reader.ReadSingle(), reader.ReadSingle());
	}

}