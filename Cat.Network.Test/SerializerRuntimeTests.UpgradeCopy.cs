using System.Buffers.Binary;
using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	[TestCase(false)]
	[TestCase(true)]
	public void UpgradeCopy_GeneratedMigrationRenamesListsAndDictionaries(bool empty) {
		TypeCatalogue catalogue = RegisterUpgradeCopyTypes();
		LegacyUpgradeCollectionsState source = new() { Marker = 42 };
		if (!empty) {
			source.Values.Add(3);
			source.Values.Add(5);
			source.Labels.Add(7, "seven");
			source.Labels.Add(8, "");
			source.Labels.Add(9, "Mira 猫");
		}
		byte[] payload = Serialize(source, catalogue, MemberIdentificationMode.Name);
		RenamedUpgradeCollectionsState target = new();
		target.Items.Add(-1);
		target.Names.Add(-1, "replaced");

		Deserialize(target, catalogue, payload);

		Assert.Multiple(() => {
			Assert.That(BinaryPrimitives.ReadUInt16LittleEndian(payload), Is.EqualTo(1));
			Assert.That(target.Marker, Is.EqualTo(42));
			Assert.That(target.Items, Is.EqualTo(source.Values));
			Assert.That(target.Names, Is.EquivalentTo(source.Labels));
		});
		AssertRenamedUpgradeBytesArePreserved(payload, catalogue);
	}

	[TestCase(false)]
	[TestCase(true)]
	public void UpgradeCopy_GeneratedMigrationPreservesNullableObjectsAndNestedCollections(bool nullProperties) {
		TypeCatalogue catalogue = RegisterUpgradeCopyTypes();
		LegacyUpgradeCollectionsState source = new() {
			OptionalValue = nullProperties ? null : 17,
			Child = nullProperties ? null : new DirtyChildState { Value = 23 }
		};
		source.NullableValues.Add(null);
		source.NullableValues.Add(31);
		source.Children.Add(null);
		source.Children.Add(CreateUpgradeNestedCollection(37));
		source.ChildLookup.Add(1, null);
		source.ChildLookup.Add(2, CreateUpgradeNestedCollection(41));
		byte[] payload = Serialize(source, catalogue, MemberIdentificationMode.Name);
		RenamedUpgradeCollectionsState target = new() {
			OptionalCount = -1,
			Detail = new DirtyChildState { Value = -1 }
		};

		Deserialize(target, catalogue, payload);

		Assert.Multiple(() => {
			Assert.That(target.OptionalCount, Is.EqualTo(source.OptionalValue));
			Assert.That(target.Detail?.Value, Is.EqualTo(source.Child?.Value));
			Assert.That(target.OptionalItems, Is.EqualTo(new int?[] { null, 31 }));
			Assert.That(target.NestedItems, Has.Count.EqualTo(2));
			Assert.That(target.NestedItems[0], Is.Null);
			Assert.That(target.NestedItems[1]!.Values, Is.EqualTo(new int?[] { null, 37 }));
			Assert.That(target.NestedItems[1]!.Labels[37], Is.EqualTo("nested 猫"));
			Assert.That(target.NestedLookup, Has.Count.EqualTo(2));
			Assert.That(target.NestedLookup[1], Is.Null);
			Assert.That(target.NestedLookup[2]!.Values, Is.EqualTo(new int?[] { null, 41 }));
			Assert.That(target.NestedLookup[2]!.Labels[41], Is.EqualTo("nested 猫"));
		});
		AssertRenamedUpgradeBytesArePreserved(payload, catalogue);
	}

	[Test]
	public void UpgradeCopy_CopyExceptStillPreservesUnchangedGeneratedCollectionPayloads() {
		TypeCatalogue catalogue = RegisterUpgradeCopyTypes();
		LegacyUpgradeCollectionsState source = new() { Marker = 42 };
		source.Values.Add(3);
		source.Values.Add(5);
		source.Labels.Add(7, "seven");
		byte[] payload = Serialize(source, catalogue, MemberIdentificationMode.Name);
		SerializationContext context = new(catalogue);
		NetworkObjectUpgradeReader reader = ReadUpgradeFields(payload.AsSpan(2), context);
		NetworkObjectUpgradeWriter writer = new(new BufferWriter(), reader, context);

		UnchangedUpgradeCollectionsState.UpgradeToVersion2(reader, writer);
		UnchangedUpgradeCollectionsState target = new();
		Deserialize(target, catalogue, payload);

		Assert.Multiple(() => {
			Assert.That(writer.Complete(), Is.EqualTo(payload[2..]));
			Assert.That(target.Marker, Is.EqualTo(42));
			Assert.That(target.Values, Is.EqualTo(new[] { 3, 5 }));
			Assert.That(target.Labels, Is.EquivalentTo(source.Labels));
		});
	}

	[Test]
	public void UpgradeCopy_TypedCollectionReadsAndWritesRemainUnsupported() {
		TypeCatalogue catalogue = RegisterUpgradeCopyTypes();
		LegacyUpgradeCollectionsState source = new();
		source.Values.Add(3);
		source.Labels.Add(7, "seven");
		SerializationContext context = new(catalogue);
		NetworkObjectUpgradeReader reader = ReadUpgradeFields(Serialize(source, catalogue, MemberIdentificationMode.Name).AsSpan(2), context);
		NetworkObjectUpgradeWriter writer = new(new BufferWriter(), reader, context);

		Assert.Multiple(() => {
			Assert.That(() => reader.Get<NetworkList<int>>("Values"), Throws.InvalidOperationException.With.Message.Contains("is not supported"));
			Assert.That(() => reader.Get<NetworkDictionary<int, string>>("Labels"), Throws.InvalidOperationException.With.Message.Contains("is not supported"));
			Assert.That(() => writer.Write("Items", source.Values), Throws.InvalidOperationException.With.Message.Contains("is not supported"));
			Assert.That(() => writer.Write("Names", source.Labels), Throws.InvalidOperationException.With.Message.Contains("is not supported"));
			Assert.That(ReadUpgradeFields(writer.Complete(), context).Fields, Is.Empty);
		});
	}

	[TestCase(null, "Items", "sourceName")]
	[TestCase("", "Items", "sourceName")]
	[TestCase(" \t", "Items", "sourceName")]
	[TestCase("Values", null, "destinationName")]
	[TestCase("Values", "", "destinationName")]
	[TestCase("Values", " \t", "destinationName")]
	public void UpgradeCopy_RejectsInvalidNamesWithoutWriting(string? sourceName, string? destinationName, string parameterName) {
		SerializationContext context = new(RegisterTypes());
		NetworkObjectUpgradeReader reader = ReadUpgradeFields(BuildObjectPayload(MemberIdentificationMode.Name, BuildNameField("Values", Int32(3))).AsSpan(2), context);
		NetworkObjectUpgradeWriter writer = new(new BufferWriter(), reader, context);

		Assert.That(() => writer.Copy(sourceName!, destinationName!), Throws.InstanceOf<ArgumentException>().With.Property("ParamName").EqualTo(parameterName));
		Assert.That(ReadUpgradeFields(writer.Complete(), context).Fields, Is.Empty);
	}

	[Test]
	public void UpgradeCopy_RequiresASourceReader() {
		SerializationContext context = new(RegisterTypes());
		NetworkObjectUpgradeWriter writer = new(new BufferWriter(), null, context);

		Assert.That(() => writer.Copy("Values", "Items"), Throws.InvalidOperationException.With.Message.EqualTo("Copy requires a source upgrade reader."));
		Assert.That(ReadUpgradeFields(writer.Complete(), context).Fields, Is.Empty);
	}

	[TestCase("Missing")]
	[TestCase("values")]
	public void UpgradeCopy_RequiresAnOrdinalSourceMatch(string sourceName) {
		SerializationContext context = new(RegisterTypes());
		NetworkObjectUpgradeReader reader = ReadUpgradeFields(BuildObjectPayload(MemberIdentificationMode.Name, BuildNameField("Values", Int32(3))).AsSpan(2), context);
		NetworkObjectUpgradeWriter writer = new(new BufferWriter(), reader, context);

		Assert.Multiple(() => {
			Assert.That(() => writer.Copy(sourceName, "Items"), Throws.TypeOf<KeyNotFoundException>().With.Message.EqualTo($"Upgrade payload does not contain a field named '{sourceName}'."));
			Assert.That(() => reader.Get<int>(sourceName), Throws.TypeOf<KeyNotFoundException>());
			Assert.That(reader.TryGet<int>(sourceName, out _), Is.False);
			Assert.That(ReadUpgradeFields(writer.Complete(), context).Fields, Is.Empty);
		});
	}

	[Test]
	public void UpgradeCopy_UsesTheLastDuplicateAndDoesNotRemoveOrReplaceFields() {
		SerializationContext context = new(RegisterTypes());
		NetworkObjectUpgradeReader reader = ReadUpgradeFields(BuildObjectPayload(MemberIdentificationMode.Name,
			BuildNameField("Values", Int32(1)),
			BuildNameField("values", Int32(2)),
			BuildNameField("Values", Int32(3))).AsSpan(2), context);
		NetworkObjectUpgradeWriter writer = new(new BufferWriter(), reader, context);
		writer.CopyExcept("Values");
		writer.Write("Items", 0);
		writer.Copy("Values", "Items");
		writer.Copy("values", "Éléments");
		writer.Copy("Values", "Values");
		NetworkObjectUpgradeReader output = ReadUpgradeFields(writer.Complete(), context);

		Assert.Multiple(() => {
			Assert.That(reader.Get<int>("Values"), Is.EqualTo(3));
			Assert.That(reader.TryGet("Values", out int value), Is.True);
			Assert.That(value, Is.EqualTo(3));
			Assert.That(reader.Fields, Has.Count.EqualTo(3));
			Assert.That(output.Fields.Select(field => field.Name), Is.EqualTo(new[] { "values", "Items", "Items", "Éléments", "Values" }));
			Assert.That(output.Get<int>("Items"), Is.EqualTo(3));
			Assert.That(output.Get<int>("Éléments"), Is.EqualTo(2));
			Assert.That(output.Get<int>("Values"), Is.EqualTo(3));
			Assert.That(output.Fields[1].Value.ToArray(), Is.EqualTo(Int32(0)));
			Assert.That(output.Fields[2].Value.ToArray(), Is.EqualTo(reader.Fields[2].Value.ToArray()));
		});
	}

	[Test]
	public void UpgradeCopy_CopiesOpaqueEmptyValuesFromTheReaderSnapshot() {
		SerializationContext context = new(RegisterTypes());
		byte[] payload = BuildObjectPayload(MemberIdentificationMode.Name, BuildNameField("Empty", []));
		NetworkObjectUpgradeReader reader = ReadUpgradeFields(payload.AsSpan(2), context);
		Array.Fill(payload, (byte)0xff);
		NetworkObjectUpgradeWriter writer = new(new BufferWriter(), reader, context);
		writer.Copy("Empty", "Renamed");
		NetworkObjectUpgradeReader output = ReadUpgradeFields(writer.Complete(), context);

		Assert.That(output.Fields.Single().Name, Is.EqualTo("Renamed"));
		Assert.That(output.Fields.Single().Value.IsEmpty, Is.True);
	}

	[Test]
	public void UpgradeCopy_RejectsWritesAfterCompletionAndKeepsCompletionIdempotent() {
		SerializationContext context = new(RegisterTypes());
		NetworkObjectUpgradeReader reader = ReadUpgradeFields(BuildObjectPayload(MemberIdentificationMode.Name, BuildNameField("Values", Int32(3))).AsSpan(2), context);
		NetworkObjectUpgradeWriter writer = new(new BufferWriter(), reader, context);
		writer.Copy("Values", "Items");
		byte[] completed = writer.Complete();

		Assert.Multiple(() => {
			Assert.That(() => writer.Copy("Values", "Later"), Throws.InvalidOperationException.With.Message.Contains("completed"));
			Assert.That(() => writer.Write("Later", 5), Throws.InvalidOperationException.With.Message.Contains("completed"));
			Assert.That(() => writer.CopyExcept(), Throws.InvalidOperationException.With.Message.Contains("completed"));
			Assert.That(writer.Complete(), Is.EqualTo(completed));
			Assert.That(ReadUpgradeFields(completed, context).Fields.Single().Name, Is.EqualTo("Items"));
		});
	}

	[Test]
	public void UpgradeCopy_EnforcesTheExistingFieldCountLimit() {
		SerializationContext context = new(RegisterTypes());
		NetworkObjectUpgradeReader reader = ReadUpgradeFields(BuildObjectPayload(MemberIdentificationMode.Name, BuildNameField("V", Int32(3))).AsSpan(2), context);
		BufferWriter buffer = new();
		NetworkObjectUpgradeWriter writer = new(buffer, reader, context);
		for (int index = 0; index < ushort.MaxValue; index++) {
			writer.Copy("V", "V");
		}
		int fullLength = buffer.GetWrittenSpan().Length;

		Assert.Multiple(() => {
			Assert.That(() => writer.Copy("V", "Extra"), Throws.InvalidOperationException.With.Message.EqualTo("Cannot write more than 65535 fields."));
			Assert.That(() => writer.Write("Extra", 5), Throws.InvalidOperationException.With.Message.Contains("65535 fields"));
			Assert.That(() => writer.CopyExcept(), Throws.InvalidOperationException.With.Message.Contains("65535 fields"));
			Assert.That(buffer.GetWrittenSpan().Length, Is.EqualTo(fullLength));
			Assert.That(BinaryPrimitives.ReadUInt16LittleEndian(writer.Complete().AsSpan(1)), Is.EqualTo(ushort.MaxValue));
		});
	}

	private static TypeCatalogue RegisterUpgradeCopyTypes() {
		return RegisterTypes(typeof(LegacyUpgradeCollectionsState), typeof(RenamedUpgradeCollectionsState),
			typeof(UnchangedUpgradeCollectionsState), typeof(UpgradeNestedCollectionState), typeof(DirtyChildState));
	}

	private static UpgradeNestedCollectionState CreateUpgradeNestedCollection(int value) {
		UpgradeNestedCollectionState result = new();
		result.Values.Add(null);
		result.Values.Add(value);
		result.Labels.Add(value, "nested 猫");
		return result;
	}

	private static NetworkObjectUpgradeReader ReadUpgradeFields(ReadOnlySpan<byte> payload, SerializationContext context) {
		Assert.That(NetworkObjectUpgradeReader.TryCreate(payload, context, out NetworkObjectUpgradeReader reader), Is.True);
		return reader;
	}

	private static void AssertRenamedUpgradeBytesArePreserved(byte[] payload, TypeCatalogue catalogue) {
		SerializationContext context = new(catalogue);
		NetworkObjectUpgradeReader reader = ReadUpgradeFields(payload.AsSpan(2), context);
		NetworkObjectUpgradeWriter writer = new(new BufferWriter(), reader, context);
		RenamedUpgradeCollectionsState.UpgradeToVersion2(reader, writer);
		NetworkObjectUpgradeReader output = ReadUpgradeFields(writer.Complete(), context);
		(string Source, string Destination)[] fields = [
			("Marker", "Marker"), ("Values", "Items"), ("Labels", "Names"), ("OptionalValue", "OptionalCount"),
			("Child", "Detail"), ("NullableValues", "OptionalItems"), ("Children", "NestedItems"), ("ChildLookup", "NestedLookup")
		];

		Assert.That(output.Fields.Select(field => field.Name), Is.EquivalentTo(fields.Select(field => field.Destination)));
		foreach ((string sourceName, string destinationName) in fields) {
			Assert.That(output.Fields.Single(field => field.Name == destinationName).Value.ToArray(),
				Is.EqualTo(reader.Fields.Single(field => field.Name == sourceName).Value.ToArray()), destinationName);
		}
	}
}
