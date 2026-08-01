using System.Buffers.Binary;
using System.Text;
using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
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
}
