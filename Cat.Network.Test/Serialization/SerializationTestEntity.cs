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
	
	[PropertyChangedEvent]
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
	
	Vector3 NetworkProperty.VectorProperty { get; set; }

	Guid NetworkProperty.GuidProperty { get; set; }
	Guid? NetworkProperty.NullableGuidProperty { get; set; }
	

	int? NetworkProperty.NullableIntProperty { get; set; }

	Transform NetworkProperty.TransformProperty { get; set; }

	NetworkDataObject NetworkProperty.ReferenceTypeProperty { get; set; }
	

	List<int> NetworkCollection.MyInts { get; } = new();


	public int StringSetCount { get; private set; }
	private string BackingString { get; set; }
	string NetworkProperty.StringProperty {
		get => BackingString;
		set {
			BackingString = value;
			StringSetCount++;
		}
	}

	public bool RpcInvoked { get; private set; }

	void RPC.TestRpc(bool booleanParam, byte byteParam, short shortParam, int intParam, long longParam, ushort uShortParam, uint uIntParam, ulong uLongParam, string stringParam) {
		Assert.AreEqual(BooleanProperty, booleanParam);
		Assert.AreEqual(ByteProperty, byteParam);
		Assert.AreEqual(ShortProperty, shortParam);
		Assert.AreEqual(IntProperty, intParam);
		Assert.AreEqual(LongProperty, longParam);
		Assert.AreEqual(UShortProperty, uShortParam);
		Assert.AreEqual(UIntProperty, uIntParam);
		Assert.AreEqual(ULongProperty, uLongParam);
		Assert.AreEqual(StringProperty, stringParam);

		RpcInvoked = true;
	}


	void RPC.TestMemoryRpc(bool booleanParam, byte byteParam, short shortParam, int intParam, long longParam, ushort uShortParam, uint uIntParam, ulong uLongParam) {
		(ByteProperty, ShortProperty) = ((byte)shortParam, byteParam);
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