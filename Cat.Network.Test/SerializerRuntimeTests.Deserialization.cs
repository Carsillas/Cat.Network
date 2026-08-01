using System.Buffers.Binary;
using System.Text;
using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
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
}
