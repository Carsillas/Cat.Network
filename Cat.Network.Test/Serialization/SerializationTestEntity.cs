using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Test.Serialization;
internal partial class SerializationTestEntity : NetworkEntity {

	bool NetworkProperty.BooleanProperty { get; set; }
	byte NetworkProperty.ByteProperty { get; set; }
	short NetworkProperty.ShortProperty { get; set; }
	int NetworkProperty.IntProperty { get; set; }
	long NetworkProperty.LongProperty { get; set; }
	ushort NetworkProperty.UShortProperty { get; set; }
	uint NetworkProperty.UIntProperty { get; set; }
	ulong NetworkProperty.ULongProperty { get; set; }
	string NetworkProperty.StringProperty { get; set; }

	public bool RPCInvoked { get; private set; }

	void RPC.TestRPC(bool BooleanParam, byte ByteParam, short ShortParam, int IntParam, long LongParam, ushort UShortParam, uint UIntParam, ulong ULongParam, string StringParam) {
		Assert.AreEqual(BooleanProperty, BooleanParam);
		Assert.AreEqual(ByteProperty, ByteParam);
		Assert.AreEqual(ShortProperty, ShortParam);
		Assert.AreEqual(IntProperty, IntParam);
		Assert.AreEqual(LongProperty, LongParam);
		Assert.AreEqual(UShortProperty, UShortParam);
		Assert.AreEqual(UIntProperty, UIntParam);
		Assert.AreEqual(ULongProperty, ULongParam);
		Assert.AreEqual(StringProperty, StringParam);

		RPCInvoked = true;
	}


}
