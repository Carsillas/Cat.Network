using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	[TestCase(MemberIdentificationMode.Index)]
	[TestCase(MemberIdentificationMode.Name)]
	public void CollectionStructStrings_FullListPreservesFollowingInteger(MemberIdentificationMode mode) {
		TypeCatalogue catalogue = RegisterTypes(typeof(CollectionStructStringState));
		CollectionStructStringState source = new();
		source.Mixed.Add(new CollectionStringThenInt { Text = "hello", Value = 123 });
		source.Mixed.Add(new CollectionStringThenInt { Text = "猫🍀\0é", Value = -456 });
		CollectionStructStringState target = new();

		Deserialize(target, catalogue, Serialize(source, catalogue, mode));

		Assert.That(target.Mixed, Is.EqualTo(source.Mixed));
	}

	[TestCase(MemberIdentificationMode.Index)]
	[TestCase(MemberIdentificationMode.Name)]
	public void CollectionStructStrings_FullListsPreserveMultipleAndNestedNullableFields(MemberIdentificationMode mode) {
		TypeCatalogue catalogue = RegisterTypes(typeof(CollectionStructStringState));
		CollectionStructStringState source = new();
		source.Pairs.Add(new CollectionStringPair { First = null, Second = "", Tail = 1 });
		source.Pairs.Add(new CollectionStringPair { First = "", Second = null, Tail = 2 });
		source.Pairs.Add(new CollectionStringPair { First = "first", Second = "猫🍀\0é", Tail = 3 });
		source.Envelopes.Add(null);
		source.Envelopes.Add(default(CollectionStringEnvelope));
		source.Envelopes.Add(CreateStringEnvelope("nested", "", 4));
		source.Envelopes.Add(CreateStringEnvelope(null, "tail", 5, hasOptional: false));
		CollectionStructStringState target = new();

		Deserialize(target, catalogue, Serialize(source, catalogue, mode));

		Assert.Multiple(() => {
			Assert.That(target.Pairs, Is.EqualTo(source.Pairs));
			Assert.That(target.Envelopes, Is.EqualTo(source.Envelopes));
		});
	}

	[TestCase(MemberIdentificationMode.Index)]
	[TestCase(MemberIdentificationMode.Name)]
	public void CollectionStructStrings_FullDictionaryPreservesStructKeysAndValues(MemberIdentificationMode mode) {
		TypeCatalogue catalogue = RegisterTypes(typeof(CollectionStructStringState));
		CollectionStructStringState source = new();
		source.Map.Add(default, null);
		source.Map.Add(new CollectionStringPair { First = "same", Second = "", Tail = 1 }, CreateStringEnvelope("first", null, 2));
		source.Map.Add(new CollectionStringPair { First = "same", Second = "猫🍀\0é", Tail = 1 }, CreateStringEnvelope("", "last", 3, hasOptional: false));
		CollectionStructStringState target = new();

		Deserialize(target, catalogue, Serialize(source, catalogue, mode));

		AssertStringFieldDictionaryEquals(source, target);
	}

	[TestCase(MemberIdentificationMode.Index)]
	[TestCase(MemberIdentificationMode.Name)]
	public void CollectionStructStrings_DirtyListsPreserveAddInsertAndSet(MemberIdentificationMode mode) {
		TypeCatalogue catalogue = RegisterTypes(typeof(CollectionStructStringState));
		CollectionStructStringState source = new();
		CollectionStructStringState target = new();
		// Establish the same baseline without relying on the full collection codec.
		foreach (CollectionStructStringState state in new[] { source, target }) {
			state.Mixed.Add(new CollectionStringThenInt { Text = "old", Value = 1 });
			state.Pairs.Add(new CollectionStringPair { First = "old", Second = "pair", Tail = 2 });
			state.Envelopes.Add(CreateStringEnvelope("old", "envelope", 3));
			ClearStringFieldState(state, catalogue);
		}

		source.Mixed[0] = new CollectionStringThenInt { Text = "changed", Value = 10 };
		source.Mixed.Add(new CollectionStringThenInt { Text = "猫🍀", Value = 11 });
		source.Mixed.Insert(0, new CollectionStringThenInt { Text = "", Value = 12 });
		source.Pairs[0] = new CollectionStringPair { First = null, Second = "", Tail = 20 };
		source.Pairs.Insert(0, new CollectionStringPair { First = "", Second = "猫\0🍀", Tail = 21 });
		source.Pairs.Add(new CollectionStringPair { First = "last", Second = null, Tail = 22 });
		source.Envelopes[0] = CreateStringEnvelope(null, "changed", 30, hasOptional: false);
		source.Envelopes.Insert(0, null);
		source.Envelopes.Add(CreateStringEnvelope("猫🍀", "", 31));

		Deserialize(target, catalogue, Serialize(source, catalogue, mode, MemberSelectionMode.Dirty));

		Assert.Multiple(() => {
			Assert.That(target.Mixed, Is.EqualTo(source.Mixed));
			Assert.That(target.Pairs, Is.EqualTo(source.Pairs));
			Assert.That(target.Envelopes, Is.EqualTo(source.Envelopes));
		});
	}

	[TestCase(MemberIdentificationMode.Index)]
	[TestCase(MemberIdentificationMode.Name)]
	public void CollectionStructStrings_DirtyDictionaryPreservesAddSetAndRemove(MemberIdentificationMode mode) {
		TypeCatalogue catalogue = RegisterTypes(typeof(CollectionStructStringState));
		CollectionStructStringState source = new();
		CollectionStructStringState target = new();
		CollectionStringPair retainedKey = new() { First = "key", Second = null, Tail = 1 };
		CollectionStringPair removedKey = new() { First = "key", Second = "", Tail = 2 };
		foreach (CollectionStructStringState state in new[] { source, target }) {
			state.Map.Add(retainedKey, CreateStringEnvelope("old", "retained", 1));
			state.Map.Add(removedKey, CreateStringEnvelope("old", "removed", 2));
			ClearStringFieldState(state, catalogue);
		}

		source.Map[retainedKey] = CreateStringEnvelope(null, "updated", 3, hasOptional: false);
		source.Map.Remove(removedKey);
		source.Map.Add(new CollectionStringPair { First = "key", Second = "猫🍀\0é", Tail = 4 }, CreateStringEnvelope("", null, 4));
		source.Map.Add(new CollectionStringPair { First = "", Second = null, Tail = 5 }, null);

		Deserialize(target, catalogue, Serialize(source, catalogue, mode, MemberSelectionMode.Dirty));

		AssertStringFieldDictionaryEquals(source, target);
		Assert.That(target.Map.ContainsKey(removedKey), Is.False);
	}

	[TestCase(null, "")]
	[TestCase("", null)]
	[TestCase("猫🍀\0é", "second")]
	public void CollectionStructStrings_InteroperateWithGeneratedPropertiesAndUpgradeFields(string? first, string? second) {
		TypeCatalogue catalogue = RegisterTypes(typeof(CollectionStructStringState));
		SerializationContext context = new(catalogue);
		CollectionStringEnvelope sample = CreateStringEnvelope(first, second, 17, hasOptional: first is not null);
		byte[] expected = Concat(
			LengthPrefixedStringValue(first), LengthPrefixedStringValue(second), Int32(17),
			sample.Optional.HasValue
				? NullableStructValue(Concat(LengthPrefixedStringValue("inner"), Int32(456)))
				: NullValue(),
			LengthPrefixedStringValue(second), Int32(117));
		CollectionStructStringState source = new() { Sample = sample };
		byte[] propertyPayload = Serialize(source, catalogue, MemberIdentificationMode.Name);
		Assert.That(NetworkObjectUpgradeReader.TryCreate(propertyPayload.AsSpan(2), context, out NetworkObjectUpgradeReader propertyReader), Is.True);
		byte[] propertyValue = propertyReader.Fields.Single(field => field.Name == nameof(source.Sample)).Value.ToArray();
		Assert.That(propertyValue, Is.EqualTo(expected));
		Assert.That(propertyReader.Get<CollectionStringEnvelope>(nameof(source.Sample)), Is.EqualTo(sample));

		NetworkObjectUpgradeWriter upgradeWriter = new(new BufferWriter(), null, context);
		upgradeWriter.Write(nameof(source.Sample), sample);
		Assert.That(NetworkObjectUpgradeReader.TryCreate(upgradeWriter.Complete(), context, out NetworkObjectUpgradeReader upgradeReader), Is.True);
		byte[] upgradeValue = upgradeReader.Fields.Single().Value.ToArray();
		Assert.That(upgradeValue, Is.EqualTo(expected));

		source.Envelopes.Add(sample);
		byte[] collectionPayload = SerializeStringFieldCollection(source.Envelopes, context);
		Assert.That(collectionPayload, Is.EqualTo(CollectionPayload(CollectionClear(), CollectionAdd(NullableStructValue(expected)))));
		// Skip the count, Clear/Add opcodes, item length, and nullable struct marker.
		byte[] collectionValue = collectionPayload[11..];
		byte[] collectionAsProperty = BuildObjectPayload(MemberIdentificationMode.Name, BuildNameField(nameof(source.Sample), collectionValue));
		Assert.That(NetworkObjectUpgradeReader.TryCreate(collectionAsProperty.AsSpan(2), context, out NetworkObjectUpgradeReader collectionReader), Is.True);
		Assert.That(collectionReader.Get<CollectionStringEnvelope>(nameof(source.Sample)), Is.EqualTo(sample));
		CollectionStructStringState propertyTarget = new();
		Deserialize(propertyTarget, catalogue, collectionAsProperty);
		Assert.That(propertyTarget.Sample, Is.EqualTo(sample));

		foreach (byte[] fieldValue in new[] { propertyValue, upgradeValue }) {
			CollectionStructStringState collectionTarget = new();
			((INetworkCollection)collectionTarget.Envelopes).Deserialize(CollectionPayload(CollectionAdd(NullableStructValue(fieldValue))), context);
			Assert.That(collectionTarget.Envelopes, Is.EqualTo(new CollectionStringEnvelope?[] { sample }));
		}
	}

	[TestCaseSource(nameof(InvalidStructStringPayloads))]
	public void CollectionStructStrings_RejectInvalidFieldPayloads(byte[] fieldPayload) {
		SerializationContext context = new(new TypeCatalogue());
		NetworkValueList<CollectionStringOnly> list = InitializeStringFieldCollection(new NetworkValueList<CollectionStringOnly>());
		NetworkValueDictionary<CollectionStringOnly, int> keys = InitializeStringFieldCollection(new NetworkValueDictionary<CollectionStringOnly, int>());
		NetworkValueDictionary<int, CollectionStringOnly> values = InitializeStringFieldCollection(new NetworkValueDictionary<int, CollectionStringOnly>());

		Assert.Multiple(() => {
			Assert.Throws<InvalidOperationException>(() => ((INetworkCollection)list).Deserialize(CollectionPayload(CollectionAdd(fieldPayload)), context), "List item");
			Assert.Throws<InvalidOperationException>(() => ((INetworkCollection)keys).Deserialize(CollectionPayload(CollectionDictionaryAdd(fieldPayload, Int32(7))), context), "Dictionary key");
			Assert.Throws<InvalidOperationException>(() => ((INetworkCollection)values).Deserialize(CollectionPayload(CollectionDictionaryAdd(Int32(7), fieldPayload)), context), "Dictionary value");
		});
	}

	[Test]
	public void CollectionStructStrings_RejectEveryTruncatedMixedItem() {
		SerializationContext context = new(new TypeCatalogue());
		byte[] complete = Concat(LengthPrefixedStringValue("猫🍀"), Int32(123));
		for (int length = 0; length < complete.Length; length++) {
			NetworkValueList<CollectionStringThenInt> list = InitializeStringFieldCollection(new NetworkValueList<CollectionStringThenInt>());
			byte[] truncated = complete[..length];
			Assert.Throws<InvalidOperationException>(() => ((INetworkCollection)list).Deserialize(CollectionPayload(CollectionAdd(truncated)), context), $"Truncation at byte {length}");
		}
	}

	[TestCase(null)]
	[TestCase("")]
	[TestCase("猫🍀\0é")]
	public void CollectionStructStrings_SingleFieldConsumesOnlyItsFramedValue(string? value) {
		SerializationContext context = new(new TypeCatalogue());
		NetworkValueList<CollectionStringOnly> list = InitializeStringFieldCollection(new NetworkValueList<CollectionStringOnly>());
		byte[] payload = CollectionPayload(CollectionAdd(LengthPrefixedStringValue(value)), CollectionAdd(LengthPrefixedStringValue("next")));

		((INetworkCollection)list).Deserialize(payload, context);

		Assert.That(list.Select(item => item.Value), Is.EqualTo(new[] { value, "next" }));
		Assert.That(SerializeStringFieldCollection(list, context, MemberSelectionMode.Dirty), Is.EqualTo(payload));
	}

	[Test]
	public void CollectionStructStrings_PreserveTopLevelNonNullStringRoundTrips() {
		SerializationContext context = new(new TypeCatalogue());
		NetworkValueList<string> source = InitializeStringFieldCollection(new NetworkValueList<string>());
		NetworkValueList<string> target = InitializeStringFieldCollection(new NetworkValueList<string>());
		source.Add("");
		source.Add("猫🍀\0é");
		((INetworkCollection)target).Deserialize(SerializeStringFieldCollection(source, context), context);
		Assert.That(target, Is.EqualTo(source));

		((INetworkCollection)source).ClearDirtyState(context);
		source[0] = "changed";
		((INetworkCollection)target).Deserialize(SerializeStringFieldCollection(source, context, MemberSelectionMode.Dirty), context);
		Assert.That(target, Is.EqualTo(source));
	}

	private static IEnumerable<TestCaseData> InvalidStructStringPayloads() {
		yield return new TestCaseData(Array.Empty<byte>()).SetArgDisplayNames("missing marker");
		yield return new TestCaseData(new byte[] { 2 }).SetArgDisplayNames("invalid marker 2");
		yield return new TestCaseData(new byte[] { 255 }).SetArgDisplayNames("invalid marker 255");
		yield return new TestCaseData(new byte[] { 1 }).SetArgDisplayNames("missing length");
		yield return new TestCaseData(new byte[] { 1, 0, 0, 0 }).SetArgDisplayNames("partial length");
		yield return new TestCaseData(new byte[] { 1, 2, 0, 0, 0, 97 }).SetArgDisplayNames("truncated UTF-8 bytes");
		yield return new TestCaseData(Concat(new byte[] { 1 }, UInt32(uint.MaxValue))).SetArgDisplayNames("oversized length");
		yield return new TestCaseData(new byte[] { 0, 9 }).SetArgDisplayNames("trailing byte after null");
	}

	private static CollectionStringEnvelope CreateStringEnvelope(string? first, string? second, int tail, bool hasOptional = true) {
		return new CollectionStringEnvelope {
			Details = new CollectionStringPair { First = first, Second = second, Tail = tail },
			Optional = hasOptional ? new CollectionStringThenInt { Text = "inner", Value = 456 } : null,
			Suffix = second,
			Tail = tail + 100
		};
	}

	private static void AssertStringFieldDictionaryEquals(CollectionStructStringState source, CollectionStructStringState target) {
		Assert.That(target.Map.Count, Is.EqualTo(source.Map.Count));
		foreach ((CollectionStringPair key, CollectionStringEnvelope? value) in source.Map) {
			Assert.That(target.Map.ContainsKey(key), Is.True);
			Assert.That(target.Map[key], Is.EqualTo(value));
		}
	}

	private static void ClearStringFieldState(NetworkObject target, TypeCatalogue catalogue) {
		Assert.That(catalogue.TryFindSerializer(target.GetType(), out INetworkObjectSerializer? serializer), Is.True);
		serializer!.ClearDirtyState(target, new SerializationContext(catalogue));
	}

	private static T InitializeStringFieldCollection<T>(T collection) where T : INetworkCollection {
		collection.Initialize(new ValueCollectionState(), 0);
		return collection;
	}

	private static byte[] SerializeStringFieldCollection(INetworkCollection collection, SerializationContext context, MemberSelectionMode selection = MemberSelectionMode.All) {
		BufferWriter writer = new();
		collection.Serialize(writer, context, new SerializationOptions(selection, MemberIdentificationMode.Index));
		return writer.GetWrittenSpan().ToArray();
	}
}
