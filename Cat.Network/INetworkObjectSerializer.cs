namespace Cat.Network;

public interface INetworkObjectSerializer {
	static abstract void Serialize();
	static abstract void Deserialize();
}