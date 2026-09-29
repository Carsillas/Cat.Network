namespace Cat.Network.Test;

public sealed class InheritedNetworkMemberNameRuntimeTests {
	[TestCase(MemberIdentificationMode.Name)]
	[TestCase(MemberIdentificationMode.Index)]
	public void PrivateInheritedMembersWithDistinctNamesRoundTrip(MemberIdentificationMode identificationMode) {
		TypeCatalogue catalogue = new();
		catalogue.Register(typeof(MemberNameDerivedState));
		Assert.That(catalogue.TryFindSerializer(typeof(MemberNameDerivedState), out INetworkObjectSerializer? serializer), Is.True);
		SerializationContext context = new(catalogue);
		MemberNameDerivedState original = new() { Other = 17 };
		original.SetBaseValues(42, 56, 73);
		BufferWriter writer = new();
		serializer!.Serialize(writer, original, context, new SerializationOptions(MemberSelectionMode.All, identificationMode));

		MemberNameDerivedState restored = new();
		serializer.Deserialize(restored, writer.GetWrittenSpan(), context);

		Assert.Multiple(() => {
			Assert.That(restored.ScalarValue, Is.EqualTo(42));
			Assert.That(restored.ListValue, Is.EqualTo(56));
			Assert.That(restored.DictionaryValue, Is.EqualTo(73));
			Assert.That(restored.Other, Is.EqualTo(17));
			Assert.That(((MemberNameDerivedState)original.Clone()).ScalarValue, Is.EqualTo(42));
		});
	}
}

[NetworkObject]
public abstract partial class MemberNameBaseState : NetworkObject {
	[NetworkProperty] private partial int Scalar { get; set; }
	[NetworkCollection] private partial NetworkList<int> Values { get; }
	[NetworkCollection] private partial NetworkDictionary<int, int> Entries { get; }

	public int ScalarValue => Scalar;
	public int ListValue => Values[0];
	public int DictionaryValue => Entries[1];

	public void SetBaseValues(int scalar, int list, int dictionary) {
		Scalar = scalar;
		Values.Add(list);
		Entries.Add(1, dictionary);
	}
}

[NetworkObject]
public abstract partial class MemberNameMiddleState : MemberNameBaseState { }

[NetworkObject]
public partial class MemberNameDerivedState : MemberNameMiddleState {
	[NetworkProperty] public partial int Other { get; set; }
}
