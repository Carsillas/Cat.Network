using System;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network {
	public interface ITransport {
		void SendPacket(RequestBuffer request);

		bool TryReadPacket(out byte[] bytes);

	}
}
