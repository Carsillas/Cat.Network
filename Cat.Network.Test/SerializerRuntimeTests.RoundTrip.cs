using System.Buffers.Binary;
using System.Text;
using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	[Test]
	public void RoundTrip_PrimitiveState_PreservesIndexPayload() {
		TypeCatalogue catalogue = RegisterTypes(typeof(PrimitiveState));
		PrimitiveState original = PrimitiveState.Create(42, "Mira");

		AssertRoundTripSerializationEquals(original, new PrimitiveState(), catalogue);
	}

	[Test]
	public void RoundTrip_NullableState_PreservesIndexPayload() {
		TypeCatalogue catalogue = RegisterTypes(typeof(NullableState));
		NullableState original = NullableState.Create(99, new Guid("00112233-4455-6677-8899-aabbccddeeff"));

		AssertRoundTripSerializationEquals(original, new NullableState(), catalogue);
	}

	[Test]
	public void RoundTrip_PlayerState_PreservesIndexPayload() {
		TypeCatalogue catalogue = RegisterTypes(typeof(PlayerState));
		PlayerState original = PlayerState.Create(30, 12);

		AssertRoundTripSerializationEquals(original, new PlayerState(), catalogue);
	}

	[Test]
	public void RoundTrip_StructState_PreservesIndexPayload() {
		TypeCatalogue catalogue = RegisterTypes(typeof(StructState));
		StructState original = new() {
			PreviousStats = new Stats {
				Accuracy = 0.5f,
				Details = new DetailStats {
					CriticalChance = 1.5d,
					UltraDetails = null
				},
				Health = 3,
				Name = "Before",
				SessionId = null
			},
			Stats = new Stats {
				Accuracy = 1.5f,
				Details = new DetailStats {
					CriticalChance = 2.5d,
					UltraDetails = new UltraDetailedStats {
						Vision = 3.5d
					}
				},
				Health = 7,
				Name = "Ada",
				SessionId = new Guid("8899aabb-ccdd-eeff-0011-223344556677")
			}
		};

		AssertRoundTripSerializationEquals(original, new StructState(), catalogue);
	}

	[Test]
	public void RoundTrip_ParentState_PreservesIndexPayload() {
		TypeCatalogue catalogue = RegisterTypes(typeof(ParentState), typeof(ChildState), typeof(ReplacementChildState));
		ParentState original = ParentState.Create(ReplacementChildState.Create(5, 9));

		AssertRoundTripSerializationEquals(original, new ParentState(), catalogue);
	}

	[Test]
	public void RoundTrip_ValueCollectionState_PreservesIndexPayload() {
		TypeCatalogue catalogue = RegisterTypes(typeof(ValueCollectionState));
		ValueCollectionState original = new();
		original.Values.Add(3);
		original.Values.Add(5);
		original.Values.Add(8);

		AssertRoundTripSerializationEquals(original, new ValueCollectionState(), catalogue);
	}

	[Test]
	public void RoundTrip_PrivateCollectionState_PreservesIndexPayload() {
		TypeCatalogue catalogue = RegisterTypes(typeof(PrivateCollectionState));
		PrivateCollectionState original = new();
		original.AddValue(13);
		original.AddValue(21);
		original.AddValue(34);

		AssertRoundTripSerializationEquals(original, new PrivateCollectionState(), catalogue);
	}

	[Test]
	public void RoundTrip_ObjectCollectionState_PreservesIndexPayload() {
		TypeCatalogue catalogue = RegisterTypes(typeof(ObjectCollectionState), typeof(DirtyChildState));
		ObjectCollectionState original = new();
		original.Children.Add(new DirtyChildState { Value = 2 });
		original.Children.Add(new DirtyChildState { Value = 4 });

		AssertRoundTripSerializationEquals(original, new ObjectCollectionState(), catalogue);
	}

	[Test]
	public void RoundTrip_NullableObjectCollectionState_PreservesIndexPayload() {
		TypeCatalogue catalogue = RegisterTypes(typeof(NullableObjectCollectionState), typeof(DirtyChildState));
		NullableObjectCollectionState original = new();
		original.Children.Add(new DirtyChildState { Value = 2 });
		original.Children.Add(null);
		original.Children.Add(new DirtyChildState { Value = 4 });

		AssertRoundTripSerializationEquals(original, new NullableObjectCollectionState(), catalogue);
	}

	[Test]
	public void RoundTrip_ValueDictionaryState_PreservesIndexPayload() {
		TypeCatalogue catalogue = RegisterTypes(typeof(ValueDictionaryState));
		ValueDictionaryState original = new();
		original.Values.Add(3, "three");
		original.Values.Add(5, "five");

		AssertRoundTripSerializationEquals(original, new ValueDictionaryState(), catalogue);
	}

	[Test]
	public void RoundTrip_ObjectDictionaryState_PreservesIndexPayload() {
		TypeCatalogue catalogue = RegisterTypes(typeof(ObjectDictionaryState), typeof(DirtyChildState));
		ObjectDictionaryState original = new();
		original.Children.Add(2, new DirtyChildState { Value = 4 });
		original.Children.Add(6, new DirtyChildState { Value = 8 });

		AssertRoundTripSerializationEquals(original, new ObjectDictionaryState(), catalogue);
	}

	[Test]
	public void RoundTrip_NullableObjectDictionaryState_PreservesIndexPayload() {
		TypeCatalogue catalogue = RegisterTypes(typeof(NullableObjectDictionaryState), typeof(DirtyChildState));
		NullableObjectDictionaryState original = new();
		original.Children.Add(2, new DirtyChildState { Value = 4 });
		original.Children.Add(6, null);

		AssertRoundTripSerializationEquals(original, new NullableObjectDictionaryState(), catalogue);
	}

	[Test]
	public void RoundTrip_DeepObjectCollectionState_PreservesIndexPayload() {
		TypeCatalogue catalogue = RegisterTypes(
			typeof(DeepObjectCollectionRootState),
			typeof(DeepObjectCollectionLevelTwoState),
			typeof(DeepObjectCollectionLevelThreeState),
			typeof(DirtyChildState));
		DeepObjectCollectionRootState original = new();
		DeepObjectCollectionLevelTwoState levelTwoA = new();
		DeepObjectCollectionLevelThreeState levelThreeA = new();
		DeepObjectCollectionLevelThreeState levelThreeB = new();

		levelThreeA.Children.Add(new DirtyChildState { Value = 1 });
		levelThreeA.Children.Add(new DirtyChildState { Value = 2 });
		levelThreeB.Children.Add(new DirtyChildState { Value = 3 });
		levelTwoA.Children.Add(levelThreeA);
		levelTwoA.Children.Add(levelThreeB);
		original.Children.Add(levelTwoA);

		AssertRoundTripSerializationEquals(original, new DeepObjectCollectionRootState(), catalogue);
	}
}
