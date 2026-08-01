using System.Buffers.Binary;
using System.Text;
using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	[Test]
	public void GeneratedNetworkCollectionProperty_IsInitializedWithValueCollectionImplementation() {
		ValueCollectionState target = new();

		Assert.That(target.Values, Is.TypeOf<NetworkValueList<int>>());
	}

	[Test]
	public void PrivateNetworkCollectionProperty_IsInitializedAndUsableThroughPublicMethods() {
		TypeCatalogue catalogue = RegisterTypes(typeof(PrivateCollectionState));
		PrivateCollectionState target = new();

		target.AddValue(5);
		target.AddValue(8);

		byte[] payload = Serialize(target, catalogue);
		PrivateCollectionState deserialized = new();
		Deserialize(deserialized, catalogue, payload);

		Assert.Multiple(() => {
			Assert.That(deserialized.ValueCount, Is.EqualTo(2));
			Assert.That(deserialized.GetValue(0), Is.EqualTo(5));
			Assert.That(deserialized.GetValue(1), Is.EqualTo(8));
		});
	}

	[Test]
	public void GeneratedNetworkCollectionProperty_IsInitializedWithObjectCollectionImplementation() {
		ObjectCollectionState target = new();

		Assert.That(target.Children, Is.TypeOf<NetworkObjectList<DirtyChildState>>());
	}

	[Test]
	public void GeneratedNullableNetworkCollectionProperty_IsInitializedWithObjectCollectionImplementation() {
		NullableObjectCollectionState target = new();

		Assert.That(target.Children, Is.TypeOf<NetworkObjectList<DirtyChildState>>());
	}

	[Test]
	public void GeneratedNetworkCollectionProperty_IsInitializedWithValueDictionaryImplementation() {
		ValueDictionaryState target = new();

		Assert.That(target.Values, Is.TypeOf<NetworkValueDictionary<int, string>>());
	}

	[Test]
	public void GeneratedNetworkCollectionProperty_IsInitializedWithObjectDictionaryImplementation() {
		ObjectDictionaryState target = new();

		Assert.That(target.Children, Is.TypeOf<NetworkObjectDictionary<int, DirtyChildState>>());
	}

	[Test]
	public void GeneratedNullableNetworkCollectionProperty_IsInitializedWithObjectDictionaryImplementation() {
		NullableObjectDictionaryState target = new();

		Assert.That(target.Children, Is.TypeOf<NetworkObjectDictionary<int, DirtyChildState>>());
	}

	[Test]
	public void InheritedNetworkCollectionProperty_IsInitializedOnDerivedType() {
		InheritedCollectionDerivedState target = new();

		Assert.That(target.Values, Is.TypeOf<NetworkValueList<int>>());
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

	[Test]
	public void AddingNullObjectToCollection_DoesNotThrowOrAssignParent() {
		NullableObjectCollectionState target = new();

		target.Children.Add(null);

		Assert.Multiple(() => {
			Assert.That(target.Children, Has.Count.EqualTo(1));
			Assert.That(target.Children[0], Is.Null);
			Assert.That(((INetworkObject)target).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Modified));
		});
	}

	[Test]
	public void MutatingValueDictionary_MarksOwnerPropertyAsModified() {
		ValueDictionaryState target = new();

		target.Values.Add(7, "seven");

		Assert.That(((INetworkObject)target).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Modified));
	}

	[Test]
	public void AddingObjectToDictionary_SetsParentAndPropertyIndex() {
		ObjectDictionaryState target = new();
		DirtyChildState child = new();

		target.Children.Add(4, child);

		Assert.Multiple(() => {
			Assert.That(((INetworkObject)child).Parent, Is.SameAs(target));
			Assert.That(((INetworkObject)child).PropertyIndex, Is.EqualTo(0));
			Assert.That(((INetworkObject)target).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Modified));
		});
	}

	[Test]
	public void RemovingObjectFromDictionary_ClearsParentAndPropertyIndex() {
		ObjectDictionaryState target = new();
		DirtyChildState child = new();

		target.Children.Add(4, child);
		((INetworkObject)target).PropertyStates[0] = NetworkPropertyState.Unchanged;

		target.Children.Remove(4);

		Assert.Multiple(() => {
			Assert.That(((INetworkObject)child).Parent, Is.Null);
			Assert.That(((INetworkObject)child).PropertyIndex, Is.EqualTo(-1));
			Assert.That(((INetworkObject)target).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Modified));
		});
	}

	[Test]
	public void AddingNullObjectToDictionary_DoesNotThrowOrAssignParent() {
		NullableObjectDictionaryState target = new();

		target.Children.Add(4, null);

		Assert.Multiple(() => {
			Assert.That(target.Children.ContainsKey(4), Is.True);
			Assert.That(target.Children[4], Is.Null);
			Assert.That(((INetworkObject)target).PropertyStates[0], Is.EqualTo(NetworkPropertyState.Modified));
		});
	}

	[Test]
	public void Serialize_ValueCollection_UsesCollectionOperations() {
		TypeCatalogue catalogue = RegisterTypes(typeof(ValueCollectionState));
		ValueCollectionState target = new();
		target.Values.Add(7);
		target.Values.Add(9);

		byte[] payload = Serialize(target, catalogue);

		Assert.That(payload, Is.EqualTo(BuildObjectPayload(
			BuildIndexField(0, CollectionPayload(
				CollectionClear(),
				CollectionAdd(Int32(7)),
				CollectionAdd(Int32(9)))))));
	}

	[Test]
	public void Serialize_DirtyValueCollection_UsesBufferedOperations() {
		TypeCatalogue catalogue = RegisterTypes(typeof(ValueCollectionState));
		ValueCollectionState target = new();
		target.Values.Add(7);

		byte[] payload = Serialize(target, catalogue, MemberIdentificationMode.Index, MemberSelectionMode.Dirty);

		Assert.That(payload, Is.EqualTo(BuildObjectPayload(
			BuildIndexField(0, CollectionPayload(
				CollectionAdd(Int32(7)))))));
	}

	[Test]
	public void Serialize_ValueDictionary_UsesCollectionOperations() {
		TypeCatalogue catalogue = RegisterTypes(typeof(ValueDictionaryState));
		ValueDictionaryState target = new();
		target.Values.Add(7, "seven");
		target.Values.Add(9, "nine");

		byte[] payload = Serialize(target, catalogue);

		Assert.That(payload, Is.EqualTo(BuildObjectPayload(
			BuildIndexField(0, CollectionPayload(
				CollectionClear(),
				CollectionDictionaryAdd(Int32(7), Utf8("seven")),
				CollectionDictionaryAdd(Int32(9), Utf8("nine")))))));
	}

	[Test]
	public void Serialize_DirtyValueDictionary_UsesBufferedOperations() {
		TypeCatalogue catalogue = RegisterTypes(typeof(ValueDictionaryState));
		ValueDictionaryState target = new();
		target.Values.Add(7, "seven");

		byte[] payload = Serialize(target, catalogue, MemberIdentificationMode.Index, MemberSelectionMode.Dirty);

		Assert.That(payload, Is.EqualTo(BuildObjectPayload(
			BuildIndexField(0, CollectionPayload(
				CollectionDictionaryAdd(Int32(7), Utf8("seven")))))));
	}

	[Test]
	public void Deserialize_ValueCollection_AppliesOperations() {
		TypeCatalogue catalogue = RegisterTypes(typeof(ValueCollectionState));
		ValueCollectionState target = new();

		byte[] payload = BuildObjectPayload(
			BuildIndexField(0, CollectionPayload(
				CollectionClear(),
				CollectionAdd(Int32(3)),
				CollectionAdd(Int32(5)),
				CollectionInsert(1, Int32(4)),
				CollectionSet(0, Int32(2)),
				CollectionRemove(2))));
		Deserialize(target, catalogue, payload);

		Assert.That(target.Values, Is.EqualTo(new[] { 2, 4 }));
	}

	[Test]
	public void Deserialize_ValueDictionary_AppliesOperations() {
		TypeCatalogue catalogue = RegisterTypes(typeof(ValueDictionaryState));
		ValueDictionaryState target = new();

		byte[] payload = BuildObjectPayload(
			BuildIndexField(0, CollectionPayload(
				CollectionClear(),
				CollectionDictionaryAdd(Int32(3), Utf8("three")),
				CollectionDictionaryAdd(Int32(5), Utf8("five")),
				CollectionDictionarySet(Int32(3), Utf8("THREE")),
				CollectionDictionaryRemove(Int32(5)))));
		Deserialize(target, catalogue, payload);

		Assert.Multiple(() => {
			Assert.That(target.Values.Count, Is.EqualTo(1));
			Assert.That(target.Values[3], Is.EqualTo("THREE"));
		});
	}

	[Test]
	public void Serialize_DirtyObjectCollection_UsesUpdateOperationForDirtyItem() {
		TypeCatalogue catalogue = RegisterTypes(typeof(ObjectCollectionState), typeof(DirtyChildState));
		ObjectCollectionState target = new();

		byte[] initialPayload = BuildObjectPayload(
			BuildIndexField(0, CollectionPayload(
				CollectionClear(),
				CollectionAdd(ObjectItem(
					typeof(DirtyChildState),
					BuildObjectPayload(
						BuildIndexField(0, Int32(1))))))));
		Deserialize(target, catalogue, initialPayload);

		DirtyChildState child = (DirtyChildState)target.Children.Single();
		child.Value = 5;

		byte[] payload = Serialize(target, catalogue, MemberIdentificationMode.Index, MemberSelectionMode.Dirty);

		byte[] expectedPayload = BuildObjectPayload(
			BuildIndexField(0, CollectionPayload(
				CollectionUpdate(0, BuildObjectPayload(
					BuildIndexField(0, Int32(5)))))));
		Assert.That(payload, Is.EqualTo(expectedPayload));
	}

	[Test]
	public void Serialize_DirtyObjectDictionary_UsesUpdateOperationForDirtyValue() {
		TypeCatalogue catalogue = RegisterTypes(typeof(ObjectDictionaryState), typeof(DirtyChildState));
		ObjectDictionaryState target = new();

		byte[] initialPayload = BuildObjectPayload(
			BuildIndexField(0, CollectionPayload(
				CollectionClear(),
				CollectionDictionaryAdd(
					Int32(2),
					ObjectItem(
						typeof(DirtyChildState),
						BuildObjectPayload(
							BuildIndexField(0, Int32(1))))))));
		Deserialize(target, catalogue, initialPayload);

		DirtyChildState child = target.Children[2];
		child.Value = 5;

		byte[] payload = Serialize(target, catalogue, MemberIdentificationMode.Index, MemberSelectionMode.Dirty);

		byte[] expectedPayload = BuildObjectPayload(
			BuildIndexField(0, CollectionPayload(
				CollectionDictionaryUpdate(Int32(2), BuildObjectPayload(
					BuildIndexField(0, Int32(5)))))));
		Assert.That(payload, Is.EqualTo(expectedPayload));
	}

	[Test]
	public void Serialize_DirtyNestedObjectCollections_UsesUpdateOperationsAtEachLevel() {
		TypeCatalogue catalogue = RegisterTypes(
			typeof(DeepObjectCollectionRootState),
			typeof(DeepObjectCollectionLevelTwoState),
			typeof(DeepObjectCollectionLevelThreeState),
			typeof(DirtyChildState));
		DeepObjectCollectionRootState target = new();

		byte[] leafInitialPayload = BuildObjectPayload(
			BuildIndexField(0, Int32(1)));
		byte[] levelThreeInitialPayload = BuildObjectPayload(
			BuildIndexField(0, CollectionPayload(
				CollectionClear(),
				CollectionAdd(ObjectItem(typeof(DirtyChildState), leafInitialPayload)))));
		byte[] levelTwoInitialPayload = BuildObjectPayload(
			BuildIndexField(0, CollectionPayload(
				CollectionClear(),
				CollectionAdd(ObjectItem(typeof(DeepObjectCollectionLevelThreeState), levelThreeInitialPayload)))));
		byte[] initialPayload = BuildObjectPayload(
			BuildIndexField(0, CollectionPayload(
				CollectionClear(),
				CollectionAdd(ObjectItem(typeof(DeepObjectCollectionLevelTwoState), levelTwoInitialPayload)))));
		Deserialize(target, catalogue, initialPayload);

		DirtyChildState leaf = (DirtyChildState)((DeepObjectCollectionLevelThreeState)((DeepObjectCollectionLevelTwoState)target.Children.Single()).Children.Single()).Children.Single();
		leaf.Value = 5;

		byte[] payload = Serialize(target, catalogue, MemberIdentificationMode.Index, MemberSelectionMode.Dirty);

		byte[] leafExpectedPayload = BuildObjectPayload(
			BuildIndexField(0, Int32(5)));
		byte[] levelThreeExpectedPayload = BuildObjectPayload(
			BuildIndexField(0, CollectionPayload(
				CollectionUpdate(0, leafExpectedPayload))));
		byte[] levelTwoExpectedPayload = BuildObjectPayload(
			BuildIndexField(0, CollectionPayload(
				CollectionUpdate(0, levelThreeExpectedPayload))));
		byte[] expectedPayload = BuildObjectPayload(
			BuildIndexField(0, CollectionPayload(
				CollectionUpdate(0, levelTwoExpectedPayload))));
		Assert.That(payload, Is.EqualTo(expectedPayload));
	}
}
