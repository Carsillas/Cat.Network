namespace Cat.Network;

public interface IEntitySerializer {

	void Serialize(Stream stream, NetworkEntity entity);
	
}