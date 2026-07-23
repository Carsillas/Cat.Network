using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;

namespace Cat.Network;

public class RelayServer(IDaemon daemon) {
	
	private IDaemon Daemon { get; } = daemon ?? throw new ArgumentNullException(nameof(daemon));
	
	private List<IRelayTransport> Transports { get; } = [];
	private const int GuidSize = 16;

	
	
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

	private void ProcessMessage(IRelayTransport sender, ReadOnlySpan<byte> message) {
		if (!TryExtractPacketChannel(ref message, out NetworkMessageChannel channel)) {
			return;
		}

		switch (channel) {
			case NetworkMessageChannel.Application:
				break;
			case NetworkMessageChannel.EntityMessage:
				ProcessEntityMessage(sender, message);
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}
	}

	private void ProcessEntityMessage(IRelayTransport sender, ReadOnlySpan<byte> message) {
		if (!TryExtractEntityMessageKind(ref message, out EntityMessageKind kind)) {
			return;
		}
		
		if (!TryExtractEntityId(ref message, out Guid entityId)) {
			return;
		}
		
		switch (kind) {
			case EntityMessageKind.Create:
				
			case EntityMessageKind.Update:
				
				break;
			case EntityMessageKind.Delete:
				break;
			case EntityMessageKind.Rpc:
				break;
			case EntityMessageKind.Broadcast:
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}
		
	}
	
	private static bool TryExtractPacketChannel(ref ReadOnlySpan<byte> message, out NetworkMessageChannel channel) {
		channel = default;

		if (message.Length < sizeof(NetworkMessageChannel)) {
			return false;
		}

		channel = (NetworkMessageChannel)message[0];
		message = message[sizeof(NetworkMessageChannel)..];
		
		return true;
	}

	private static bool TryExtractEntityMessageKind(ref ReadOnlySpan<byte> message, out EntityMessageKind kind) {
		kind = default;

		if (message.Length < sizeof(EntityMessageKind)) {
			return false;
		}

		kind = (EntityMessageKind)message[0];
		message = message[sizeof(EntityMessageKind)..];

		return true;
	}

	private static bool TryExtractEntityId(ref ReadOnlySpan<byte> message, out Guid guid) {
		guid = Guid.Empty;
		
		if (message.Length < GuidSize) {
			return false;
		}
		
		guid = new Guid(message[..GuidSize]);
		message = message[GuidSize..];
		
		return true;
	}

	private static bool TryExtractTypeName(ref ReadOnlySpan<byte> message, out string? name) {
		name = null;

		if (message.Length < sizeof(int)) {
			return false;
		}
		
		int stringByteCount = BinaryPrimitives.ReadInt32LittleEndian(message);
		message = message[sizeof(int)..];

		if (message.Length < stringByteCount) {
			return false;
		}
		
		name = Encoding.UTF8.GetString(message[..stringByteCount]);
		
		return true;
	}

	private static bool ApplyChanges(NetworkObject target, ref ReadOnlySpan<byte> message) {
		
	}
	
}
