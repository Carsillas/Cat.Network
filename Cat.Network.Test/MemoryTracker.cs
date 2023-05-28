using System;

namespace Cat.Network.Test;

public struct MemoryTracker : IDisposable {
	private long MemoryBefore { get; }
	private long MemoryAfter { get; set; }
	private long MemoryAfterCollect { get; set; }
	public long Difference => MemoryAfter - MemoryBefore;
	public long Garbage => MemoryAfter - MemoryAfterCollect;

	public bool SuppressOutput { get; }

	public MemoryTracker(bool SuppressOutput = false) {
		GC.Collect();
		MemoryBefore = GC.GetTotalMemory(true);
		MemoryAfter = MemoryBefore;
		MemoryAfterCollect = MemoryBefore;
		this.SuppressOutput = SuppressOutput;
	}
	public MemoryTracker() {
		GC.Collect();
		MemoryBefore = GC.GetTotalMemory(true);
		MemoryAfter = MemoryBefore;
		MemoryAfterCollect = MemoryBefore;
		SuppressOutput = false;
	}

	public void Dispose() {
		MemoryAfter = GC.GetTotalMemory(false);
		GC.Collect();
		MemoryAfterCollect = GC.GetTotalMemory(true);
		if (!SuppressOutput) {
			Console.WriteLine($"Difference: {Difference}\nGarbage:{Garbage}");
		}
	}
}