using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Test;
internal class MemoryTests {

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
