using System;
using System.IO;

namespace Cat.Network.Properties {
	public abstract class SerializableProperty {

		public Guid Id { get; }
		public string Name { get; }


		public abstract void Serialize(BinaryWriter writer);
		public abstract void Deserialize(BinaryReader reader);

	}
}
