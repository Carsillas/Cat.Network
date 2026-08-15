using System.Buffers.Binary;
using System.Text;
using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	[Test]
	public void Deserialize_OlderNamePayload_UsesSequentialUpgradeMethod() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RenamedPrimitiveState));
		RenamedPrimitiveState target = new();

		Deserialize(target, catalogue, BuildObjectPayload(
			1,
			MemberIdentificationMode.Name,
			BuildNameField("Health", Int32(42)),
			BuildNameField("Name", StringValue("Mira"))));

		Assert.Multiple(() => {
			Assert.That(target.Health, Is.EqualTo(42));
			Assert.That(target.DisplayName, Is.EqualTo("Mira"));
		});
	}

	[Test]
	public void Deserialize_OlderNamePayload_UsesMultipleUpgradeStepsAndCopiesSupportedValues() {
		TypeCatalogue catalogue = RegisterTypes(typeof(VersionedComplexState), typeof(ChildState), typeof(ReplacementChildState));
		VersionedComplexState target = new();
		Guid sessionId = new("00112233-4455-6677-8899-aabbccddeeff");

		Deserialize(target, catalogue, BuildObjectPayload(
			1,
			MemberIdentificationMode.Name,
			BuildNameField("Health", Int32(42)),
			BuildNameField("SessionId", NullableValue(GuidBytes(sessionId))),
			BuildNameField("Stats", BuildStatsPayload(
				accuracy: 1.5f,
				criticalChance: 2.5d,
				ultraVision: 3.5d,
				health: 7,
				name: "Ada",
				sessionId: sessionId)),
			BuildNameField("Values", CollectionPayload(
				CollectionClear(),
				CollectionAdd(Int32(3)),
				CollectionAdd(Int32(5)))),
			BuildNameField("LegacyChild", ReplaceObject(
				GetTypeId(typeof(ReplacementChildState)),
				BuildObjectPayload(
					BuildIndexField(0, Int32(8)),
					BuildIndexField(1, Int32(13))))),
			BuildNameField("Name", StringValue("First")),
			BuildNameField("Name", StringValue("Mira"))));

		Assert.Multiple(() => {
			Assert.That(target.Health, Is.EqualTo(42));
			Assert.That(target.DisplayName, Is.EqualTo("Mira"));
			Assert.That(target.SessionId, Is.EqualTo(sessionId));
			Assert.That(target.Stats.Accuracy, Is.EqualTo(1.5f));
			Assert.That(target.Stats.Details.CriticalChance, Is.EqualTo(2.5d));
			Assert.That(target.Stats.Details.UltraDetails.HasValue, Is.True);
			Assert.That(target.Stats.Details.UltraDetails!.Value.Vision, Is.EqualTo(3.5d));
			Assert.That(target.Stats.Health, Is.EqualTo(7));
			Assert.That(target.Stats.Name, Is.EqualTo("Ada"));
			Assert.That(target.Stats.SessionId, Is.EqualTo(sessionId));
			Assert.That(target.Values, Is.EqualTo(new[] { 3, 5 }));
			Assert.That(target.Child, Is.TypeOf<ReplacementChildState>());
			Assert.That(target.Child!.Value, Is.EqualTo(8));
			Assert.That(((ReplacementChildState)target.Child).Bonus, Is.EqualTo(13));
		});
	}

	[Test]
	public void Deserialize_OlderNamePayload_WithMissingUpgradeStep_DoesNotMutateTarget() {
		TypeCatalogue catalogue = RegisterTypes(typeof(MissingSequentialUpgradeState));
		MissingSequentialUpgradeState target = new() {
			Health = 5
		};

		Deserialize(target, catalogue, BuildObjectPayload(
			1,
			MemberIdentificationMode.Name,
			BuildNameField("Health", Int32(42))));

		Assert.That(target.Health, Is.EqualTo(5));
	}

	[Test]
	public void Deserialize_OlderIndexPayload_ThrowsWithoutMutatingTarget() {
		TypeCatalogue catalogue = RegisterTypes(typeof(RenamedPrimitiveState));
		RenamedPrimitiveState target = new() {
			DisplayName = "Existing",
			Health = 5
		};

		Assert.That(
			() => Deserialize(target, catalogue, BuildObjectPayload(
				1,
				MemberIdentificationMode.Index,
				BuildIndexField(0, Int32(42)),
				BuildIndexField(1, StringValue("Mira")))),
			Throws.TypeOf<InvalidOperationException>()
				.With.Message.EqualTo("Index-mode payloads do not support NetworkObject version upgrades."));

		Assert.Multiple(() => {
			Assert.That(target.Health, Is.EqualTo(5));
			Assert.That(target.DisplayName, Is.EqualTo("Existing"));
		});
	}

	[Test]
	public void UpgradeWriter_WriteUnsupportedValueType_Throws() {
		BufferWriter buffer = new();
		NetworkObjectUpgradeWriter writer = new(buffer, null, new SerializationContext(RegisterTypes()));

		Assert.That(
			() => writer.Write("Timestamp", DateTime.UnixEpoch),
			Throws.TypeOf<InvalidOperationException>()
				.With.Message.EqualTo("Upgrade field type 'System.DateTime' is not supported."));
	}
}
