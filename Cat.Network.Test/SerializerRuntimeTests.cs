using System.Buffers.Binary;
using System.Text;
using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed class SerializerRuntimeTests {
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
	public void Serialize_PrimitiveAndStringProperties_InIndexMode() {
		TypeCatalogue catalogue = RegisterTypes(typeof(PrimitiveState));
		PrimitiveState target = PrimitiveState.Create(42, "Mira");

		byte[] payload = Serialize(target, catalogue);

		Assert.That(payload, Is.EqualTo(BuildObjectPayload(
			BuildIndexField(0, Int32(42)),
			BuildIndexField(1, Utf8("Mira")))));
	}

	[Test]
	public void Serialize_PrimitiveAndStringProperties_InNameMode() {
		TypeCatalogue catalogue = RegisterTypes(typeof(PrimitiveState));
		PrimitiveState target = PrimitiveState.Create(42, "Mira");

		byte[] payload = Serialize(target, catalogue, MemberIdentificationMode.Name);

		Assert.That(payload, Is.EqualTo(BuildObjectPayload(
			MemberIdentificationMode.Name,
			BuildNameField("Health", Int32(42)),
			BuildNameField("Name", Utf8("Mira")))));
	}

	[Test]
	public void Serialize_NullableValueTypes() {
		TypeCatalogue catalogue = RegisterTypes(typeof(NullableState));
		Guid sessionId = new("00112233-4455-6677-8899-aabbccddeeff");
		NullableState target = NullableState.Create(99, sessionId);

		byte[] payload = Serialize(target, catalogue);

		Assert.That(payload, Is.EqualTo(BuildObjectPayload(
			BuildIndexField(0, NullableValue(Int32(99))),
			BuildIndexField(1, NullableValue(GuidBytes(sessionId))))));

		byte[] nullPayload = Serialize(NullableState.Create(null, null), catalogue);

		Assert.That(nullPayload, Is.EqualTo(BuildObjectPayload(
			BuildIndexField(0, NullValue()),
			BuildIndexField(1, NullValue()))));
	}

	[Test]
	public void Serialize_NestedStructs_AndNullableNestedStructs() {
		TypeCatalogue catalogue = RegisterTypes(typeof(StructState));
		Guid sessionId = new("8899aabb-ccdd-eeff-0011-223344556677");
		StructState target = new() {
			PreviousStats = null,
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
				SessionId = sessionId
			}
		};

		byte[] payload = Serialize(target, catalogue);

		Assert.That(payload, Is.EqualTo(BuildObjectPayload(
			BuildIndexField(0, NullValue()),
			BuildIndexField(1, BuildStatsPayload(
				accuracy: 1.5f,
				criticalChance: 2.5d,
				ultraVision: 3.5d,
				health: 7,
				name: "Ada",
				sessionId: sessionId)))));
	}

	[Test]
	public void Serialize_NetworkObject_UsesReplaceAndClear() {
		TypeCatalogue catalogue = RegisterTypes(typeof(ParentState), typeof(ChildState), typeof(ReplacementChildState));
		Guid replacementTypeId = GetTypeId(typeof(ReplacementChildState));
		ParentState target = ParentState.Create(ReplacementChildState.Create(5, 9));

		byte[] payload = Serialize(target, catalogue);

		Assert.That(payload, Is.EqualTo(BuildObjectPayload(
			BuildIndexField(0, ReplaceObject(
				replacementTypeId,
				BuildObjectPayload(
					BuildIndexField(0, Int32(5)),
					BuildIndexField(1, Int32(9))))))));

		byte[] clearPayload = Serialize(ParentState.Create(null), catalogue);

		Assert.That(clearPayload, Is.EqualTo(BuildObjectPayload(
			BuildIndexField(0, ClearObject()))));
	}

	[Test]
	public void Deserialize_PrimitiveAndStringProperties_InIndexMode() {
		TypeCatalogue catalogue = RegisterTypes(typeof(PrimitiveState));
		PrimitiveState target = new();

		Deserialize(target, catalogue, BuildObjectPayload(
			BuildIndexField(0, Int32(42)),
			BuildIndexField(1, Utf8("Mira"))));

		Assert.Multiple(() => {
			Assert.That(target.Health, Is.EqualTo(42));
			Assert.That(target.Name, Is.EqualTo("Mira"));
		});
	}

	[Test]
	public void Deserialize_InheritedProperties_OnDerivedType() {
		TypeCatalogue catalogue = RegisterTypes(typeof(PlayerState));
		PlayerState target = new();

		Deserialize(target, catalogue, BuildObjectPayload(
			BuildIndexField(0, Int32(30)),
			BuildIndexField(1, Int32(12))));

		Assert.Multiple(() => {
			Assert.That(target.Health, Is.EqualTo(30));
			Assert.That(target.Mana, Is.EqualTo(12));
		});
	}

	[Test]
	public void Deserialize_NullableValueTypes() {
		TypeCatalogue catalogue = RegisterTypes(typeof(NullableState));
		NullableState target = new();
		Guid sessionId = new("00112233-4455-6677-8899-aabbccddeeff");

		Deserialize(target, catalogue, BuildObjectPayload(
			BuildIndexField(0, NullableValue(Int32(99))),
			BuildIndexField(1, NullableValue(GuidBytes(sessionId)))));

		Assert.Multiple(() => {
			Assert.That(target.Health, Is.EqualTo(99));
			Assert.That(target.SessionId, Is.EqualTo(sessionId));
		});

		Deserialize(target, catalogue, BuildObjectPayload(
			BuildIndexField(0, NullValue()),
			BuildIndexField(1, NullValue())));

		Assert.Multiple(() => {
			Assert.That(target.Health, Is.Null);
			Assert.That(target.SessionId, Is.Null);
		});
	}

	[Test]
	public void Deserialize_NestedStructs_AndNullableNestedStructs() {
		TypeCatalogue catalogue = RegisterTypes(typeof(StructState));
		StructState target = new();
		Guid sessionId = new("8899aabb-ccdd-eeff-0011-223344556677");

		byte[] statsPayload = BuildStatsPayload(
			accuracy: 1.5f,
			criticalChance: 2.5d,
			ultraVision: 3.5d,
			health: 7,
			name: "Ada",
			sessionId: sessionId);

		Deserialize(target, catalogue, BuildObjectPayload(
			BuildIndexField(0, NullValue()),
			BuildIndexField(1, statsPayload)));

		Assert.Multiple(() => {
			Assert.That(target.PreviousStats, Is.Null);
			Assert.That(target.Stats.Accuracy, Is.EqualTo(1.5f));
			Assert.That(target.Stats.Details.CriticalChance, Is.EqualTo(2.5d));
			Assert.That(target.Stats.Details.UltraDetails.HasValue, Is.True);
			Assert.That(target.Stats.Details.UltraDetails!.Value.Vision, Is.EqualTo(3.5d));
			Assert.That(target.Stats.Health, Is.EqualTo(7));
			Assert.That(target.Stats.Name, Is.EqualTo("Ada"));
			Assert.That(target.Stats.SessionId, Is.EqualTo(sessionId));
		});
	}

	[Test]
	public void Deserialize_NetworkObject_ModifyReplaceAndClear() {
		TypeCatalogue catalogue = RegisterTypes(typeof(ParentState), typeof(ChildState), typeof(ReplacementChildState));
		ParentState target = new();

		Guid replacementTypeId = GetTypeId(typeof(ReplacementChildState));
		byte[] replacementObjectData = BuildObjectPayload(
			BuildIndexField(0, Int32(5)),
			BuildIndexField(1, Int32(9)));

		Deserialize(target, catalogue, BuildObjectPayload(
			BuildIndexField(0, ReplaceObject(replacementTypeId, replacementObjectData))));

		Assert.Multiple(() => {
			Assert.That(target.Child, Is.TypeOf<ReplacementChildState>());
			Assert.That(target.Child!.Value, Is.EqualTo(5));
			Assert.That(((ReplacementChildState)target.Child).Bonus, Is.EqualTo(9));
		});

		byte[] modifyObjectData = BuildObjectPayload(
			BuildIndexField(0, Int32(8)),
			BuildIndexField(1, Int32(11)));

		Deserialize(target, catalogue, BuildObjectPayload(
			BuildIndexField(0, ModifyObject(modifyObjectData))));

		Assert.Multiple(() => {
			Assert.That(target.Child, Is.TypeOf<ReplacementChildState>());
			Assert.That(target.Child!.Value, Is.EqualTo(8));
			Assert.That(((ReplacementChildState)target.Child).Bonus, Is.EqualTo(11));
		});

		Deserialize(target, catalogue, BuildObjectPayload(
			BuildIndexField(0, ClearObject())));

		Assert.That(target.Child, Is.Null);
	}

	[Test]
	public void Deserialize_InvalidNullableFlag_DoesNotMutateTarget() {
		TypeCatalogue catalogue = RegisterTypes(typeof(NullableState));
		NullableState target = new();

		Deserialize(target, catalogue, BuildObjectPayload(
			BuildIndexField(0, NullableValue(Int32(1)))));

		Deserialize(target, catalogue, BuildObjectPayload(
			BuildIndexField(0, new byte[] { 2 })));

		Assert.That(target.Health, Is.EqualTo(1));
	}

	[Test]
	public void Deserialize_InvalidStructPayload_DoesNotAssignPartialState() {
		TypeCatalogue catalogue = RegisterTypes(typeof(StructState));
		StructState target = new();

		Deserialize(target, catalogue, BuildObjectPayload(
			BuildIndexField(1, BuildStatsPayload(
				accuracy: 0.5f,
				criticalChance: 1.5d,
				ultraVision: null,
				health: 3,
				name: "Before",
				sessionId: null))));

		Stats original = target.Stats;
		byte[] invalidPayload = Concat(
			BuildStatsPayload(
				accuracy: 9.5f,
				criticalChance: 8.5d,
				ultraVision: 7.5d,
				health: 6,
				name: "Broken",
				sessionId: Guid.Parse("12345678-1234-5678-1234-567812345678")),
			new byte[] { 0xFF });

		Deserialize(target, catalogue, BuildObjectPayload(
			BuildIndexField(1, invalidPayload)));

		Assert.Multiple(() => {
			Assert.That(target.Stats.Accuracy, Is.EqualTo(original.Accuracy));
			Assert.That(target.Stats.Details.CriticalChance, Is.EqualTo(original.Details.CriticalChance));
			Assert.That(target.Stats.Details.UltraDetails.HasValue, Is.EqualTo(original.Details.UltraDetails.HasValue));
			Assert.That(target.Stats.Health, Is.EqualTo(original.Health));
			Assert.That(target.Stats.Name, Is.EqualTo(original.Name));
			Assert.That(target.Stats.SessionId, Is.EqualTo(original.SessionId));
		});
	}

	[Test]
	public void Assigning_NetworkObjectProperty_SetsAndClearsParent() {
		ParentAssignmentState parent = new();
		ChildState firstChild = ChildState.Create(1);
		ChildState secondChild = ChildState.Create(2);

		parent.Child = firstChild;
		Assert.Multiple(() => {
			Assert.That(((INetworkObject)firstChild).Parent, Is.SameAs(parent));
			Assert.That(((INetworkObject)firstChild).PropertyIndex, Is.EqualTo(0));
		});

		parent.Child = secondChild;

		Assert.Multiple(() => {
			Assert.That(((INetworkObject)firstChild).Parent, Is.Null);
			Assert.That(((INetworkObject)firstChild).PropertyIndex, Is.EqualTo(-1));
			Assert.That(((INetworkObject)secondChild).Parent, Is.SameAs(parent));
			Assert.That(((INetworkObject)secondChild).PropertyIndex, Is.EqualTo(0));
		});

		parent.Child = null;

		Assert.Multiple(() => {
			Assert.That(((INetworkObject)secondChild).Parent, Is.Null);
			Assert.That(((INetworkObject)secondChild).PropertyIndex, Is.EqualTo(-1));
		});
	}

	[Test]
	public void Assigning_NetworkObjectProperty_ThrowsWhenChildAlreadyHasParent() {
		ParentAssignmentState firstParent = new();
		ParentAssignmentState secondParent = new();
		ChildState child = ChildState.Create(1);

		firstParent.Child = child;

		Assert.That(() => secondParent.Child = child, Throws.TypeOf<InvalidOperationException>());
		Assert.That(((INetworkObject)child).Parent, Is.SameAs(firstParent));
		Assert.That(secondParent.Child, Is.Null);
	}

	[Test]
	public void Assigning_NetworkObjectProperty_ThrowsWhenChildAlreadyOccupiesDifferentPropertyOnSameParent() {
		ParentAssignmentState parent = new();
		ChildState child = ChildState.Create(1);

		parent.Child = child;

		Assert.That(() => parent.SecondaryChild = child, Throws.TypeOf<InvalidOperationException>());

		Assert.Multiple(() => {
			Assert.That(parent.Child, Is.SameAs(child));
			Assert.That(parent.SecondaryChild, Is.Null);
			Assert.That(((INetworkObject)child).Parent, Is.SameAs(parent));
			Assert.That(((INetworkObject)child).PropertyIndex, Is.EqualTo(0));
		});
	}

	[Test]
	public void Setting_Property_MarksOwnStateAsReplaced() {
		DirtyPrimitiveState target = new();

		target.Value = 9;

		Assert.That(((INetworkObject)target).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Replaced));
	}

	[Test]
	public void Setting_ChildProperty_MarksParentStateAsModified() {
		DirtyParentState parent = new();
		DirtyChildState child = new();

		parent.Child = child;
		((INetworkObject)parent).PropertyStates[0] = NetworkPropertyState.Unchanged;

		child.Value = 9;

		Assert.Multiple(() => {
			Assert.That(((INetworkObject)child).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Replaced));
			Assert.That(((INetworkObject)parent).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Modified));
		});
	}

	[Test]
	public void Setting_ChildProperty_MarksAllAncestorsAsModified() {
		DirtyParentState grandParent = new();
		DirtyParentState parent = new();
		DirtyChildState child = new();

		((INetworkObject)parent).Parent = grandParent;
		((INetworkObject)parent).PropertyIndex = 0;
		parent.Child = child;
		((INetworkObject)grandParent).PropertyStates = new[] { NetworkPropertyState.Unchanged };
		((INetworkObject)parent).PropertyStates[0] = NetworkPropertyState.Unchanged;

		child.Value = 5;

		Assert.Multiple(() => {
			Assert.That(((INetworkObject)child).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Replaced));
			Assert.That(((INetworkObject)parent).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Modified));
			Assert.That(((INetworkObject)grandParent).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Modified));
		});
	}

	[Test]
	public void Serialize_DirtyMode_EmitsOnlyDirtyFields() {
		TypeCatalogue catalogue = RegisterTypes(typeof(DirtyPairState));
		DirtyPairState target = new();

		target.First = 10;
		target.Second = 20;
		((INetworkObject)target).PropertyStates = new[] {
			NetworkPropertyState.Unchanged,
			NetworkPropertyState.Unchanged
		};

		target.Second = 25;

		byte[] payload = Serialize(target, catalogue, MemberIdentificationMode.Index, MemberSelectionMode.Dirty);

		Assert.That(payload, Is.EqualTo(BuildObjectPayload(
			BuildIndexField(1, Int32(25)))));
	}

	[Test]
	public void Serialize_DirtyMode_WithNoDirtyFields_EmitsZeroFieldCount() {
		TypeCatalogue catalogue = RegisterTypes(typeof(DirtyPrimitiveState));
		DirtyPrimitiveState target = new();

		byte[] payload = Serialize(target, catalogue, MemberIdentificationMode.Index, MemberSelectionMode.Dirty);

		Assert.That(payload, Is.EqualTo(BuildObjectPayload()));
	}

	[Test]
	public void Serialize_DirtyMode_NestedModifiedObject_UsesModifyPayload() {
		TypeCatalogue catalogue = RegisterTypes(typeof(DirtyParentState), typeof(DirtyChildState));
		DirtyParentState parent = new();
		DirtyChildState child = new();

		parent.Child = child;
		((INetworkObject)parent).PropertyStates[0] = NetworkPropertyState.Unchanged;
		((INetworkObject)child).PropertyStates[0] = NetworkPropertyState.Unchanged;

		child.Value = 9;

		byte[] payload = Serialize(parent, catalogue, MemberIdentificationMode.Index, MemberSelectionMode.Dirty);

		Assert.That(payload, Is.EqualTo(BuildObjectPayload(
			BuildIndexField(0, ModifyObject(
				BuildObjectPayload(
					BuildIndexField(0, Int32(9))))))));
	}

	[Test]
	public void GeneratedNetworkCollectionProperty_IsInitializedWithValueCollectionImplementation() {
		ValueCollectionState target = new();

		Assert.That(target.Values, Is.TypeOf<NetworkValueList<int>>());
	}

	[Test]
	public void GeneratedNetworkCollectionProperty_IsInitializedWithObjectCollectionImplementation() {
		ObjectCollectionState target = new();

		Assert.That(target.Children, Is.TypeOf<NetworkObjectList<DirtyChildState>>());
	}

	[Test]
	public void MutatingValueCollection_MarksOwnerPropertyAsModified() {
		ValueCollectionState target = new();

		target.Values.Add(7);

		Assert.That(((INetworkObject)target).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Modified));
	}

	[Test]
	public void AddingObjectToCollection_SetsParentAndPropertyIndex() {
		ObjectCollectionState target = new();
		DirtyChildState child = new();

		target.Children.Add(child);

		Assert.Multiple(() => {
			Assert.That(((INetworkObject)child).Parent, Is.SameAs(target));
			Assert.That(((INetworkObject)child).PropertyIndex, Is.EqualTo(0));
			Assert.That(((INetworkObject)target).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Modified));
		});
	}

	[Test]
	public void RemovingObjectFromCollection_ClearsParentAndPropertyIndex() {
		ObjectCollectionState target = new();
		DirtyChildState child = new();

		target.Children.Add(child);
		((INetworkObject)target).PropertyStates[0] = NetworkPropertyState.Unchanged;

		target.Children.Remove(child);

		Assert.Multiple(() => {
			Assert.That(((INetworkObject)child).Parent, Is.Null);
			Assert.That(((INetworkObject)child).PropertyIndex, Is.EqualTo(-1));
			Assert.That(((INetworkObject)target).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Modified));
		});
	}

	private static void Deserialize(NetworkObject target, TypeCatalogue catalogue, byte[] payload) {
		Assert.That(catalogue.TryFindSerializer(target.GetType(), out INetworkObjectSerializer? serializer), Is.True);
		serializer!.Deserialize(target, payload, new SerializationContext(catalogue));
	}

	private static void AssertRoundTripSerializationEquals<T>(T original, T roundTripTarget, TypeCatalogue catalogue)
		where T : NetworkObject {
		byte[] firstPayload = Serialize(original, catalogue);
		Deserialize(roundTripTarget, catalogue, firstPayload);
		byte[] secondPayload = Serialize(roundTripTarget, catalogue);

		Assert.That(secondPayload, Is.EqualTo(firstPayload));
	}

	private static byte[] Serialize(
		NetworkObject target,
		TypeCatalogue catalogue,
		MemberIdentificationMode memberIdentificationMode = MemberIdentificationMode.Index,
		MemberSelectionMode memberSelectionMode = MemberSelectionMode.All) {
		Assert.That(catalogue.TryFindSerializer(target.GetType(), out INetworkObjectSerializer? serializer), Is.True);
		BufferWriter writer = new();
		serializer!.Serialize(writer, target, new SerializationContext(catalogue), new SerializationOptions(memberSelectionMode, memberIdentificationMode));
		return writer.GetWrittenSpan().ToArray();
	}

	private static TypeCatalogue RegisterTypes(params Type[] types) {
		TypeCatalogue catalogue = new();
		foreach (Type type in types) {
			catalogue.Register(type);
		}

		return catalogue;
	}

	private static Guid GetTypeId(Type type) {
		return type.GetCustomAttributes(typeof(NetworkObjectTypeId), inherit: false)
			.OfType<NetworkObjectTypeId>()
			.Single()
			.Id;
	}

	private static byte[] BuildObjectPayload(params byte[][] fields) {
		return BuildObjectPayload(MemberIdentificationMode.Index, fields);
	}

	private static byte[] BuildObjectPayload(MemberIdentificationMode memberIdentificationMode, params byte[][] fields) {
		return Concat(
			new[] { (byte)memberIdentificationMode },
			UInt16((ushort)fields.Length),
			Concat(fields));
	}

	private static byte[] BuildIndexField(ushort index, byte[] value) {
		return Concat(UInt16(index), UInt32((uint)value.Length), value);
	}

	private static byte[] BuildNameField(string name, byte[] value) {
		byte[] nameBytes = Utf8(name);
		return Concat(UInt32((uint)nameBytes.Length), nameBytes, UInt32((uint)value.Length), value);
	}

	private static byte[] BuildStatsPayload(float accuracy, double criticalChance, double? ultraVision, int health, string name, Guid? sessionId) {
		return Concat(
			Single(accuracy),
			Double(criticalChance),
			ultraVision.HasValue
				? NullableStructValue(Double(ultraVision.Value))
				: NullValue(),
			Int32(health),
			LengthPrefixedUtf8(name),
			sessionId.HasValue
				? NullableValue(GuidBytes(sessionId.Value))
				: NullValue());
	}

	private static byte[] ModifyObject(byte[] objectData) {
		return Concat(new[] { (byte)NetworkObjectUpdateMode.Modify }, objectData);
	}

	private static byte[] ReplaceObject(Guid typeId, byte[] objectData) {
		return Concat(new[] { (byte)NetworkObjectUpdateMode.Replace }, GuidBytes(typeId), objectData);
	}

	private static byte[] ClearObject() {
		return new[] { (byte)NetworkObjectUpdateMode.Clear };
	}

	private static byte[] NullableValue(byte[] value) {
		return Concat(new byte[] { 1 }, value);
	}

	private static byte[] NullableStructValue(byte[] structBytes) {
		return Concat(new byte[] { 1 }, structBytes);
	}

	private static byte[] NullValue() {
		return new byte[] { 0 };
	}

	private static byte[] LengthPrefixedUtf8(string value) {
		byte[] utf8 = Utf8(value);
		return Concat(UInt32((uint)utf8.Length), utf8);
	}

	private static byte[] Utf8(string value) {
		return Encoding.UTF8.GetBytes(value);
	}

	private static byte[] GuidBytes(Guid value) {
		return value.ToByteArray();
	}

	private static byte[] UInt16(ushort value) {
		byte[] bytes = new byte[2];
		BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
		return bytes;
	}

	private static byte[] UInt32(uint value) {
		byte[] bytes = new byte[4];
		BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
		return bytes;
	}

	private static byte[] Int32(int value) {
		byte[] bytes = new byte[4];
		BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
		return bytes;
	}

	private static byte[] Single(float value) {
		byte[] bytes = new byte[4];
		BinaryPrimitives.WriteSingleLittleEndian(bytes, value);
		return bytes;
	}

	private static byte[] Double(double value) {
		byte[] bytes = new byte[8];
		BinaryPrimitives.WriteDoubleLittleEndian(bytes, value);
		return bytes;
	}

	private static byte[] Concat(params byte[][] arrays) {
		int totalLength = arrays.Sum(static array => array.Length);
		byte[] result = new byte[totalLength];
		int offset = 0;

		foreach (byte[] array in arrays) {
			Buffer.BlockCopy(array, 0, result, offset, array.Length);
			offset += array.Length;
		}

		return result;
	}
}
