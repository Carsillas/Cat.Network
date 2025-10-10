using System;
using System.Collections.Generic;

namespace Cat.Network;
public interface ISerializationContext {
	bool DeserializeDirtiesProperty { get; }
	bool IsDeserializing { get; }
	
	int Time { get; }

	Span<byte> RentRpcBuffer(NetworkEntity entity);
	Span<byte> RentBroadcastBuffer(NetworkEntity entity);

	internal List<byte[]> GetOutgoingRpcs(NetworkEntity entity);
	internal List<byte[]> GetOutgoingBroadcasts(NetworkEntity entity);

	void MarkForClean(INetworkEntity entity);

}
