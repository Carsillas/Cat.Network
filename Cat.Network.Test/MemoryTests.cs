using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Test;
internal class MemoryTests {

	private struct MemoryTracker : IDisposable {
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

	[SetUp]
	public void Setup() {
		// For some reason the first instance of MemoryTracker reports 8-16k
		using MemoryTracker tracker = new MemoryTracker(true);
	}


	[Test]
	public void StringSerializationTest() {
		string s = "Hello, world!";
		byte[] bytes = new byte[s.Length * 2];

		using (new MemoryTracker()) {
			Encoding.Unicode.GetBytes(s, new Span<byte>(bytes));
			Encoding.Unicode.GetBytes(s, 0, s.Length, bytes, 0);
		}
	}


}
