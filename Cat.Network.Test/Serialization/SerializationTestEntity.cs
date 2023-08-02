using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Cat.Network.Test.Serialization;

public partial class SerializationTestEntity : NetworkEntity {

	bool NetworkProperty.BooleanProperty { get; set; }
	byte NetworkProperty.ByteProperty { get; set; }
	short NetworkProperty.ShortProperty { get; set; }
	int NetworkProperty.IntProperty { get; set; }
	long NetworkProperty.LongProperty { get; set; }
	ushort NetworkProperty.UShortProperty { get; set; }
	uint NetworkProperty.UIntProperty { get; set; }
	ulong NetworkProperty.ULongProperty { get; set; }
	float NetworkProperty.FloatProperty { get; set; }
	double NetworkProperty.DoubleProperty { get; set; }
	CustomEnum NetworkProperty.EnumProperty { get; set; }


	int? NetworkProperty.NullableIntProperty { get; set; }

	Transform NetworkProperty.TransformProperty { get; set; }


	List<int> NetworkCollection.MyInts { get; } = new List<int>();


	public int StringSetCount { get; private set; }
	private string BackingString { get; set; }
	string NetworkProperty.StringProperty {
		get => BackingString;
		set {
			BackingString = value;
			StringSetCount++;
		}
	}

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


	void RPC.TestMemoryRPC(bool BooleanParam, byte ByteParam, short ShortParam, int IntParam, long LongParam, ushort UShortParam, uint UIntParam, ulong ULongParam) {
		(ByteProperty, ShortProperty) = ((byte)ShortParam, ByteParam);
	}

	void RPC.TestTransformSerialization(Transform transform) {
		Assert.AreEqual(TransformProperty, transform);
	}

}


public struct Transform {

	public Vector3? Position;
	public Vector3 Scale;

	public Vector3 Rotation { get; set; }

	public Vector3 RotationTest => Rotation;
	public Vector3 RotationTest2 { get => Rotation; set => Rotation = value; }

}