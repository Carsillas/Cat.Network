namespace Cat.Network;

public delegate void MessageHandler(IRelayTransport sender, ReadOnlySpan<byte> message);

public interface IRelayTransport {
	
	void Send(ReadOnlySpan<byte> message);
	
	event MessageHandler MessageReceived; 
	event Action<IRelayTransport>? Disconnected;
	
	void PumpMessages();
}
