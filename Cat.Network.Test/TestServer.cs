using NUnit.Framework;
using System.Collections.Generic;

namespace Cat.Network.Test;
public class TestServer : CatServer {
	public TestServer(IEntityStorage entityStorage) : base(entityStorage) {	}

	private List<TestTransport> Transports { get; } = new List<TestTransport>();

	public void AddTransport(TestTransport transport) {
		Transports.Add(transport);
		AddTransport(transport, new TestProfileEntity { });
	}

	public void RemoveTransport(TestTransport transport) {
		Transports.Remove(transport);
		base.RemoveTransport(transport);
	}

	protected override void PreExecute() {
		base.PreExecute();

		foreach(TestTransport transport in Transports) {
			foreach (byte[] message in transport.Messages) {
				DeliverPacket(transport, message);
			}
			transport.Messages.Clear();
		}

	}

}
