using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	[Test]
	public void Clone_AbstractNetworkObjectSubclassesRefineReturnType() {
		Assert.Multiple(() => {
			Assert.That(typeof(NetworkEntity).GetMethod(nameof(NetworkObject.Clone))!.ReturnType, Is.EqualTo(typeof(NetworkEntity)));
			Assert.That(typeof(NetworkProfile).GetMethod(nameof(NetworkObject.Clone))!.ReturnType, Is.EqualTo(typeof(NetworkProfile)));
		});
	}

	[Test]
	public void AbstractNetworkObjectTypes_DoNotHaveGeneratedSerializerMetadata() {
		Assert.Multiple(() => {
			Assert.That(Attribute.GetCustomAttribute(typeof(NetworkObject), typeof(NetworkObjectAttribute), inherit: false), Is.Null);
			Assert.That(Attribute.GetCustomAttribute(typeof(NetworkObject), typeof(NetworkObjectTypeId), inherit: false), Is.Null);
			Assert.That(Attribute.GetCustomAttribute(typeof(NetworkObject), typeof(NetworkObjectSerializerAttribute), inherit: false), Is.Null);
			Assert.That(Attribute.GetCustomAttribute(typeof(NetworkEntity), typeof(NetworkObjectTypeId), inherit: false), Is.Null);
			Assert.That(Attribute.GetCustomAttribute(typeof(NetworkEntity), typeof(NetworkObjectSerializerAttribute), inherit: false), Is.Null);
			Assert.That(Attribute.GetCustomAttribute(typeof(NetworkProfile), typeof(NetworkObjectTypeId), inherit: false), Is.Null);
			Assert.That(Attribute.GetCustomAttribute(typeof(NetworkProfile), typeof(NetworkObjectSerializerAttribute), inherit: false), Is.Null);
		});
	}

	[Test]
	public void NetworkObject_ExposesPublicNonOverridableAnchorGetter() {
		System.Reflection.PropertyInfo? anchorProperty = typeof(NetworkObject).GetProperty(nameof(NetworkObject.Anchor));

		Assert.Multiple(() => {
			Assert.That(anchorProperty, Is.Not.Null);
			Assert.That(anchorProperty!.PropertyType, Is.EqualTo(typeof(NetworkObject)));
			Assert.That(anchorProperty!.GetMethod, Is.Not.Null);
			Assert.That(anchorProperty.GetMethod!.IsPublic, Is.True);
			Assert.That(anchorProperty.GetMethod.IsFinal, Is.True);
		});
	}

	[Test]
	public void NetworkObject_ExposesPublicNonOverridableIsOwnerGetter() {
		System.Reflection.PropertyInfo? isOwnerProperty = typeof(NetworkObject).GetProperty(nameof(NetworkObject.IsOwner));

		Assert.Multiple(() => {
			Assert.That(isOwnerProperty, Is.Not.Null);
			Assert.That(isOwnerProperty!.PropertyType, Is.EqualTo(typeof(bool)));
			Assert.That(isOwnerProperty!.GetMethod, Is.Not.Null);
			Assert.That(isOwnerProperty.GetMethod!.IsPublic, Is.True);
			Assert.That(isOwnerProperty.GetMethod.IsVirtual, Is.False);
		});
	}

	[Test]
	public void Clone_ReturnsContainingTypeAndCopiesNetworkProperties() {
		PrimitiveState source = PrimitiveState.Create(12, "Ada");
		source.IgnoredValue = 99;

		PrimitiveState clone = source.Clone();

		Assert.Multiple(() => {
			Assert.That(clone, Is.Not.SameAs(source));
			Assert.That(clone.Health, Is.EqualTo(12));
			Assert.That(clone.Name, Is.EqualTo("Ada"));
			Assert.That(clone.IgnoredValue, Is.EqualTo(0));
		});
	}

	[Test]
	public void Clone_DeepCopiesNetworkObjectProperties() {
		ReplacementChildState child = ReplacementChildState.Create(3, 7);
		ParentState source = ParentState.Create(child);

		ParentState clone = source.Clone();

		Assert.Multiple(() => {
			Assert.That(clone.Child, Is.Not.Null);
			Assert.That(clone.Child, Is.Not.SameAs(source.Child));
			Assert.That(clone.Child, Is.TypeOf<ReplacementChildState>());
			Assert.That(clone.Child!.Value, Is.EqualTo(3));
			Assert.That(((ReplacementChildState)clone.Child).Bonus, Is.EqualTo(7));
			Assert.That(((INetworkObject)clone.Child).Parent, Is.SameAs(clone));
		});
	}

	[Test]
	public void Clone_CopiesInheritedNetworkProperties() {
		PlayerState source = PlayerState.Create(4, 8);

		PlayerState clone = source.Clone();

		Assert.Multiple(() => {
			Assert.That(clone, Is.Not.SameAs(source));
			Assert.That(clone.Health, Is.EqualTo(4));
			Assert.That(clone.Mana, Is.EqualTo(8));
		});
	}

	[Test]
	public void Clone_CopiesStructsByValue() {
		Guid sessionId = Guid.NewGuid();
		StructState source = new() {
			Stats = new Stats {
				Accuracy = 0.75f,
				Details = new DetailStats {
					CriticalChance = 0.25,
					UltraDetails = new UltraDetailedStats {
						Vision = 100
					}
				},
				Health = 9,
				Name = "Scout",
				SessionId = sessionId
			}
		};

		StructState clone = source.Clone();
		source.Stats = new Stats {
			Health = 1,
			Name = "Changed"
		};

		Assert.Multiple(() => {
			Assert.That(clone.Stats.Accuracy, Is.EqualTo(0.75f));
			Assert.That(clone.Stats.Details.CriticalChance, Is.EqualTo(0.25));
			Assert.That(clone.Stats.Details.UltraDetails!.Value.Vision, Is.EqualTo(100));
			Assert.That(clone.Stats.Health, Is.EqualTo(9));
			Assert.That(clone.Stats.Name, Is.EqualTo("Scout"));
			Assert.That(clone.Stats.SessionId, Is.EqualTo(sessionId));
		});
	}

	[Test]
	public void Clone_DoesNotCopyNetworkCollections() {
		ValueCollectionState source = new();
		source.Values.Add(1);
		source.Values.Add(2);

		ValueCollectionState clone = source.Clone();

		Assert.That(clone.Values, Is.Empty);
	}

	[Test]
	public void Clone_DoesNotCopyNetworkPropertyStates() {
		DirtyPairState source = new();
		source.First = 1;
		source.Second = 2;
		((INetworkObject)source).PropertyStates[0] = NetworkPropertyState.Unchanged;
		((INetworkObject)source).PropertyStates[1] = NetworkPropertyState.Modified;

		DirtyPairState clone = source.Clone();

		Assert.That(((INetworkObject)clone).PropertyStates, Is.EqualTo(new[] {
			NetworkPropertyState.Replaced,
			NetworkPropertyState.Replaced
		}));
	}
}
