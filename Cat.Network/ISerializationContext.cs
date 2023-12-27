using System;
using System.Collections.Generic;

namespace Cat.Network;
public interface ISerializationContext {
	bool DeserializeDirtiesProperty { get; }
	bool IsDeserializing { get; }
	
	int Time { get; }

	Span<byte> RentRpcBuffer(NetworkEntity entity);

	internal List<byte[]> GetOutgoingRpcs(NetworkEntity entity);

	void MarkForClean(INetworkEntity entity);

}
