using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	[TestCase(MemberIdentificationMode.Index)]
	[TestCase(MemberIdentificationMode.Name)]
	public void StringCollections_FullSnapshot_RoundTripsNullEmptyAndUtf8(MemberIdentificationMode identificationMode) {
		TypeCatalogue catalogue = RegisterTypes(typeof(NullableStringCollectionState));
		NullableStringCollectionState source = new();
		string?[] values = [null, "", "ordinary", "猫 🐈", "\0\u0001"];
		string[] keys = ["", "empty", "ordinary", "猫 🐈", "\0\u0001"];
		for (int index = 0; index < values.Length; index++) {
			source.Items.Add(values[index]);
			source.Values.Add(keys[index], values[index]);
		}
		NullableStringCollectionState target = new();
		target.Items.Add("stale");
		target.Values.Add("stale", "value");

		byte[] payload = Serialize(source, catalogue, identificationMode);
		Deserialize(target, catalogue, payload);

		Assert.Multiple(() => {
			Assert.That(target.Items, Is.EqualTo(values));
			Assert.That(target.Values.ToArray(), Is.EquivalentTo(source.Values.ToArray()));
			Assert.That(Serialize(target, catalogue, identificationMode), Is.EqualTo(payload));
		});
	}

	[TestCase(null)]
	[TestCase("")]
	[TestCase("ordinary")]
	[TestCase("猫 🐈")]
	public void StringCollections_DirtyList_RoundTripsAddInsertAndSet(string? value) {
		TypeCatalogue catalogue = RegisterTypes(typeof(NullableStringCollectionState));
		NullableStringCollectionState source = new();
		source.Items.Add("before");
		source.Items.Add("after");
		NullableStringCollectionState target = new();
		Deserialize(target, catalogue, Serialize(source, catalogue));
		ClearStringCollectionState(source, catalogue);

		source.Items.Add(value);
		source.Items.Insert(1, value);
		source.Items[0] = value;
		Deserialize(target, catalogue, Serialize(source, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));
		Assert.That(target.Items, Is.EqualTo(new[] { value, value, "after", value }));

		ClearStringCollectionState(source, catalogue);
		source.Items[0] = value is null ? "" : null;
		Deserialize(target, catalogue, Serialize(source, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));
		Assert.That(target.Items, Is.EqualTo(source.Items));
	}

	[TestCase(null)]
	[TestCase("")]
	[TestCase("ordinary")]
	[TestCase("猫 🐈")]
	public void StringCollections_DirtyDictionary_RoundTripsAddAndSet(string? value) {
		TypeCatalogue catalogue = RegisterTypes(typeof(NullableStringCollectionState));
		NullableStringCollectionState source = new();
		source.Values.Add("", "before");
		source.Values.Add("remove 🐈", "after");
		NullableStringCollectionState target = new();
		Deserialize(target, catalogue, Serialize(source, catalogue));
		ClearStringCollectionState(source, catalogue);

		source.Values.Add("added 🐈", value);
		source.Values[""] = value;
		source.Values["set\0\u0001"] = value;
		source.Values.Remove("remove 🐈");
		Deserialize(target, catalogue, Serialize(source, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));
		Assert.That(target.Values.ToArray(), Is.EquivalentTo(source.Values.ToArray()));

		ClearStringCollectionState(source, catalogue);
		source.Values[""] = value is null ? "" : null;
		Deserialize(target, catalogue, Serialize(source, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));
		Assert.That(target.Values.ToArray(), Is.EquivalentTo(source.Values.ToArray()));
	}

	[Test]
	public void StringCollections_NonNullableItemsAndKeys_RoundTripFullAndDirty() {
		TypeCatalogue catalogue = RegisterTypes(typeof(StringCollectionState));
		StringCollectionState source = new();
		source.Items.Add("");
		source.Items.Add("ordinary");
		source.Values.Add("", "");
		source.Values.Add("remove 🐈", "ordinary");
		StringCollectionState target = new();
		Deserialize(target, catalogue, Serialize(source, catalogue));
		Assert.Multiple(() => {
			Assert.That(target.Items, Is.EqualTo(source.Items));
			Assert.That(target.Values.ToArray(), Is.EquivalentTo(source.Values.ToArray()));
		});
		ClearStringCollectionState(source, catalogue);

		source.Items.Add("猫 🐈");
		source.Items.Insert(1, "\0\u0001");
		source.Items[0] = "changed";
		source.Values.Add("猫 🐈", "\0\u0001");
		source.Values[""] = "changed";
		source.Values.Remove("remove 🐈");
		Deserialize(target, catalogue, Serialize(source, catalogue, memberSelectionMode: MemberSelectionMode.Dirty));
		Assert.Multiple(() => {
			Assert.That(target.Items, Is.EqualTo(source.Items));
			Assert.That(target.Values.ToArray(), Is.EquivalentTo(source.Values.ToArray()));
		});
	}

	[TestCase(null)]
	[TestCase("")]
	[TestCase("ordinary")]
	[TestCase("猫 🐈")]
	public void StringCollections_WireFormat_UsesNullMarkerInsideExistingItemLength(string? value) {
		SerializationContext context = new(new TypeCatalogue());
		NullableStringCollectionState source = new();
		source.Items.Add(value);
		source.Values.Add("猫 key", value);
		byte[] listAdd = CollectionAdd(StringValue(value));
		byte[] dictionaryAdd = CollectionDictionaryAdd(StringValue("猫 key"), StringValue(value));

		Assert.Multiple(() => {
			Assert.That(SerializeStringCollection(source.Items, context, MemberSelectionMode.All),
				Is.EqualTo(CollectionPayload(CollectionClear(), listAdd)));
			Assert.That(SerializeStringCollection(source.Items, context, MemberSelectionMode.Dirty),
				Is.EqualTo(CollectionPayload(listAdd)));
			Assert.That(SerializeStringCollection(source.Values, context, MemberSelectionMode.All),
				Is.EqualTo(CollectionPayload(CollectionClear(), dictionaryAdd)));
			Assert.That(SerializeStringCollection(source.Values, context, MemberSelectionMode.Dirty),
				Is.EqualTo(CollectionPayload(dictionaryAdd)));
		});
	}

	[TestCaseSource(nameof(InvalidStringCollectionItemCases))]
	public void StringCollections_InvalidListItem_RejectsOperationBeforeMutation(
		string hex, string error, NetworkCollectionOperationType operationType) {
		TypeCatalogue catalogue = RegisterTypes(typeof(NullableStringCollectionState));
		NullableStringCollectionState target = new();
		target.Items.Add("unchanged");
		ClearStringCollectionState(target, catalogue);
		byte[] itemPayload = Convert.FromHexString(hex);
		byte[] operation = operationType switch {
			NetworkCollectionOperationType.Add => CollectionAdd(itemPayload),
			NetworkCollectionOperationType.Insert => CollectionInsert(0, itemPayload),
			NetworkCollectionOperationType.Set => CollectionSet(0, itemPayload),
			_ => throw new ArgumentOutOfRangeException(nameof(operationType))
		};

		InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(() =>
			((INetworkCollection)target.Items).Deserialize(CollectionPayload(operation), new SerializationContext(catalogue)));

		Assert.Multiple(() => {
			Assert.That(exception!.Message, Does.Contain(error));
			Assert.That(target.Items, Is.EqualTo(new[] { "unchanged" }));
			Assert.That(((INetworkObject)target).PropertyStates, Is.All.EqualTo(NetworkPropertyState.Unchanged));
		});
	}

	[TestCase("", "truncated")]
	[TestCase("02", "invalid")]
	[TestCase("FF", "invalid")]
	[TestCase("0061", "not fully consumed")]
	public void StringCollections_InvalidDictionaryString_RejectsKeyAndValue(string hex, string error) {
		TypeCatalogue catalogue = RegisterTypes(typeof(NullableStringCollectionState));
		NullableStringCollectionState target = new();
		target.Values.Add("unchanged", "value");
		ClearStringCollectionState(target, catalogue);
		byte[] invalidPayload = Convert.FromHexString(hex);
		byte[][] operations = [
			CollectionDictionaryAdd(invalidPayload, StringValue("new value")),
			CollectionDictionarySet(StringValue("unchanged"), invalidPayload)
		];

		foreach (byte[] operation in operations) {
			InvalidOperationException? exception = Assert.Throws<InvalidOperationException>(() =>
				((INetworkCollection)target.Values).Deserialize(CollectionPayload(operation), new SerializationContext(catalogue)));
			Assert.Multiple(() => {
				Assert.That(exception!.Message, Does.Contain(error));
				Assert.That(target.Values.ToArray(), Is.EqualTo(new[] { new KeyValuePair<string, string?>("unchanged", "value") }));
				Assert.That(((INetworkObject)target).PropertyStates, Is.All.EqualTo(NetworkPropertyState.Unchanged));
			});
		}
	}

	[Test]
	public void StringCollections_TruncatedListItem_IsRejected() {
		NullableStringCollectionState target = new();
		byte[] payload = CollectionPayload(CollectionAdd(StringValue("cat")))[..^1];

		Assert.Throws<ArgumentOutOfRangeException>(() =>
			((INetworkCollection)target.Items).Deserialize(payload, new SerializationContext(new TypeCatalogue())));
		Assert.That(target.Items, Is.Empty);
	}

	[Test]
	public void StringCollections_TruncatedDictionaryKeyOrValue_IsRejected() {
		NullableStringCollectionState target = new();
		byte[] truncatedKey = CollectionPayload(Concat(
			new[] { (byte)NetworkCollectionOperationType.Add }, Int32(2), new byte[] { 1 }));
		byte[] truncatedValue = CollectionPayload(CollectionDictionaryAdd(StringValue("key"), StringValue("cat")))[..^1];

		Assert.Multiple(() => {
			Assert.That(() => ((INetworkCollection)target.Values).Deserialize(truncatedKey, new SerializationContext(new TypeCatalogue())),
				Throws.InvalidOperationException.With.Message.EqualTo("Dictionary key payload is truncated."));
			Assert.That(() => ((INetworkCollection)target.Values).Deserialize(truncatedValue, new SerializationContext(new TypeCatalogue())),
				Throws.InvalidOperationException.With.Message.EqualTo("Dictionary value payload is truncated."));
			Assert.That(target.Values, Is.Empty);
		});
	}

	[Test]
	public void StringCollections_NullDictionaryKey_RemainsInvalid() {
		NullableStringCollectionState target = new();
		byte[] payload = CollectionPayload(CollectionDictionaryAdd(NullValue(), StringValue("value")));

		Assert.Throws<ArgumentNullException>(() =>
			((INetworkCollection)target.Values).Deserialize(payload, new SerializationContext(new TypeCatalogue())));
		Assert.That(target.Values, Is.Empty);
	}

	private static IEnumerable<TestCaseData> InvalidStringCollectionItemCases() {
		(string Hex, string Error)[] invalidItems = [("", "truncated"), ("02", "invalid"), ("FF", "invalid"), ("0061", "not fully consumed")];
		NetworkCollectionOperationType[] operations = [NetworkCollectionOperationType.Add, NetworkCollectionOperationType.Insert, NetworkCollectionOperationType.Set];
		foreach ((string hex, string error) in invalidItems) {
			foreach (NetworkCollectionOperationType operation in operations) {
				yield return new TestCaseData(hex, error, operation);
			}
		}
	}

	private static byte[] SerializeStringCollection(INetworkCollection collection, SerializationContext context, MemberSelectionMode selectionMode) {
		BufferWriter writer = new();
		collection.Serialize(writer, context, new SerializationOptions(selectionMode, MemberIdentificationMode.Index));
		return writer.GetWrittenSpan().ToArray();
	}

	private static void ClearStringCollectionState(NetworkObject target, TypeCatalogue catalogue) {
		Assert.That(catalogue.TryFindSerializer(target.GetType(), out INetworkObjectSerializer? serializer), Is.True);
		serializer!.ClearDirtyState(target, new SerializationContext(catalogue));
	}
}
