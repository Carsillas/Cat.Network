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

	private List<List<byte[]>> FreePools { get; } = new List<List<byte[]>>();
	private List<List<byte[]>> HeldPools { get; } = new List<List<byte[]>>();

	public byte[] RentBuffer() {
		byte[] buffer = null;
		if (FreeBuffers.Count == 0) {
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

	public List<byte[]> RentPool() {
		List<byte[]> pool = null;
		if (FreePools.Count == 0) {
			pool = CreatePool();
			HeldPools.Add(pool);
		} else {
			pool = FreePools[FreePools.Count - 1];
			FreePools.RemoveAt(FreePools.Count - 1);
			HeldPools.Add(pool);
		}
		return pool;
	}

	public void FreeAllPools() {
		foreach (List<byte[]> pool in HeldPools) {
			pool.Clear();
			FreePools.Add(pool);
		}
		HeldPools.Clear();
	}

	private byte[] CreateBuffer() {
		return new byte[BufferSize];
	}
	private List<byte[]> CreatePool() {
		return new List<byte[]>();
	}

}
