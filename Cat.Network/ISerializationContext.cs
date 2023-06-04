using System;
using System.Collections.Generic;

namespace Cat.Network;
public interface ISerializationContext {
	bool DeserializeDirtiesProperty { get; }
	int Time { get; }

	Span<byte> RentRPCBuffer(NetworkEntity entity);

	internal List<byte[]> GetOutgoingRpcs(NetworkEntity entity);

	void MarkForClean(NetworkEntity entity);

}
