using System.Buffers.Binary;
using System.Text;

namespace Cat.Network;

public class RelayServer(IDaemon daemon) : RelayPeer {
	
	private IDaemon Daemon { get; } = daemon ?? throw new ArgumentNullException(nameof(daemon));
	private List<IRelayTransport> Transports { get; } = [];
	
	private void AddTransport(IRelayTransport transport) {
		ArgumentNullException.ThrowIfNull(transport);

		if (Transports.Contains(transport)) {
			return;
		}

		Transports.Add(transport);
		transport.MessageReceived += ProcessMessage;
	}

	public void RemoveTransport(IRelayTransport transport) {
		ArgumentNullException.ThrowIfNull(transport);

		if (Transports.Remove(transport)) {
			transport.MessageReceived -= ProcessMessage;
		}
	}

	public void Tick() {
		Daemon.Tick();
		while (Daemon.TryAcceptConnection(out IRelayTransport? transport)) {
			AddTransport(transport);
		}

		foreach (IRelayTransport transport in Transports) {
			transport.PumpMessages();
		}
	}
	
}
