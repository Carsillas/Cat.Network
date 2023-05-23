﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Cat.Network.Test {
	public class TestTransport : ITransport {
		public Queue<byte[]> Messages { get; } = new Queue<byte[]>();
		public TestTransport Remote { get; set; }


		public void SendPacket(byte[] buffer, int count) {
			byte[] copy = new byte[count];
			Buffer.BlockCopy(buffer, 0, copy, 0, count);
			Remote.Messages.Enqueue(copy);
		}

	}
}
