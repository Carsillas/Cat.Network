using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	[Test]
	public void NullableCollectionFields_RoundTripNullsValuesAndFiniteGenericSiblings(
		[Values(MemberIdentificationMode.Index, MemberIdentificationMode.Name)] MemberIdentificationMode identificationMode) {
		TypeCatalogue catalogue = RegisterTypes(typeof(NullableCollectionFieldState));
		NullableCollectionFieldState source = new();
		NullableCollectionFieldValue[] values = [
			default,
			new() { Field = new NullableCollectionFieldMiddle() },
			new() {
				Field = new NullableCollectionFieldMiddle {
					First = new NullableCollectionFieldInner { Number = 42, Id = null },
					Second = new NullableCollectionFieldInner { Number = null, Id = Guid.Parse("f5d1854c-1ecf-4eb0-9655-d68a394e69a8") }
				},
				Finite = new NullableCollectionFieldBox<NullableCollectionFieldBox<int>> {
					Field = new NullableCollectionFieldBox<int> { Field = 17 }
				},
				Sibling = new NullableCollectionFieldBox<NullableCollectionFieldBox<int>> {
					Field = new NullableCollectionFieldBox<int> { Field = 29 }
				}
			}
		];
		for (int index = 0; index < values.Length; index++) {
			source.Values.Add(values[index]);
			source.Lookup.Add(index, values[index]);
		}
		source.Labels.Add(new NullableCollectionFieldKey { Label = string.Empty }, 11);
		source.Labels.Add(new NullableCollectionFieldKey { Label = "label" }, 23);

		byte[] payload = Serialize(source, catalogue, identificationMode);
		NullableCollectionFieldState target = new();
		Deserialize(target, catalogue, payload);

		Assert.Multiple(() => {
			Assert.That(target.Values, Is.EqualTo(values));
			Assert.That(target.Lookup.Count, Is.EqualTo(values.Length));
			for (int index = 0; index < values.Length; index++) {
				Assert.That(target.Lookup[index], Is.EqualTo(values[index]));
			}
			Assert.That(target.Labels[new NullableCollectionFieldKey { Label = string.Empty }], Is.EqualTo(11));
			Assert.That(target.Labels[new NullableCollectionFieldKey { Label = "label" }], Is.EqualTo(23));
			Assert.That(Serialize(target, catalogue, identificationMode), Is.EqualTo(payload));
		});
	}
}
