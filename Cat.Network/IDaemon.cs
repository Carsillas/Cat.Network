using System.Diagnostics.CodeAnalysis;

namespace Cat.Network;

public interface IDaemon {
	void Tick();
	bool TryAcceptConnection([NotNullWhen(true)] out IRelayTransport? transport, [NotNullWhen(true)] out NetworkProfile? profile);
}
