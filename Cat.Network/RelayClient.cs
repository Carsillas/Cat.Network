namespace Cat.Network;

public class RelayClient(TypeCatalogue typeCatalogue, IEntityStorage entityStorage) : RelayPeer(typeCatalogue, entityStorage) {
	
	private IRelayTransport? Transport { get; set; }

	public void Send(ReadOnlySpan<byte> message) {
		
	}

	public void Connect(IRelayTransport transport) {
		if (Transport is not null) {
			Transport.MessageReceived -= ProcessMessage;
		}

		Transport = transport ?? throw new ArgumentNullException(nameof(transport));
		Transport.MessageReceived += ProcessMessage;
	}

	public void Disconnect() {
		if (Transport is not null) {
			Transport.MessageReceived -= ProcessMessage;
		}

		Transport = null;
	}

	public void Tick() {
		if (Transport is not null) {
			Transport.PumpMessages();
		}
		

	}
}
