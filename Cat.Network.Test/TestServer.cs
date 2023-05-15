namespace Cat.Network.Test;
public class TestServer : CatServer {
	public TestServer(IEntityStorage entityStorage) : base(entityStorage) {	}

	public new void RemoveTransport(ITransport transport) {
		base.RemoveTransport(transport);
	}

}
