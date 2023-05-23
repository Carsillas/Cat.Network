using System;
using System.Collections.Generic;

namespace Cat.Network;
public interface ISerializationContext {
	bool DeserializeDirtiesProperty { get; }
	int Time { get; }

	Span<byte> RentRPCBuffer(NetworkEntity entity);

	internal IEnumerable<byte[]> GetOutgoingRpcs(NetworkEntity entity);

}
