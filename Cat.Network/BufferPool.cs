using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network;
internal class BufferPool {

	private const int BufferSize = 256 * 1024;

	private List<byte[]> FreeBuffers { get; } = new List<byte[]>();
	private List<byte[]> HeldBuffers { get; } = new List<byte[]>();

	public byte[] RentBuffer() {
		byte[] buffer = null;
		if(FreeBuffers.Count == 0) {
			buffer = CreateBuffer();
			HeldBuffers.Add(buffer);
		} else {
			buffer = FreeBuffers[FreeBuffers.Count - 1];
			FreeBuffers.RemoveAt(FreeBuffers.Count - 1);
			HeldBuffers.Add(buffer);
		}
		return buffer;
	}

	public void FreeAllBuffers() {
		foreach (byte[] buffer in HeldBuffers) {
			FreeBuffers.Add(buffer);
		}
		HeldBuffers.Clear();
	}

	private byte[] CreateBuffer() {
		return new byte[BufferSize];
	}

}
