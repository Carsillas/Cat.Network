using System.Reflection;
using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed class NetworkObjectAttachmentTests {
	[TestCase("Property", TestName = "SelfPropertyAttachmentRejectsWithoutMutation")]
	[TestCase("ListAdd", TestName = "SelfListAttachmentRejectsWithoutMutation")]
	[TestCase("ListInsert")]
	[TestCase("ListSet")]
	[TestCase("DictionaryAdd", TestName = "SelfDictionaryAttachmentRejectsWithoutMutation")]
	[TestCase("DictionarySet")]
	[TestCase("DictionaryNewKey")]
	public void SelfAttachmentRejectsWithoutMutation(string operation) {
		AttachmentNode owner = CreateDestination();
		AssertRejectedWithoutMutation(owner, owner, owner, operation);
	}

	[TestCase("Property")]
	[TestCase("ListSet")]
	[TestCase("DictionarySet")]
	public void RejectedReplacementLeavesCleanDirtyStateUnchanged(string operation) {
		AttachmentNode owner = CreateDestination();
		ClearDirtyState(owner);
		AssertRejectedWithoutMutation(owner, owner, owner, operation);
	}

	[TestCase("Property")]
	[TestCase("ListAdd")]
	[TestCase("ListInsert")]
	[TestCase("ListSet")]
	[TestCase("DictionaryAdd")]
	[TestCase("DictionarySet")]
	[TestCase("DictionaryNewKey")]
	public void MultiLevelAncestorAttachmentRejectsWithoutMutation(string operation) {
		AttachmentNode owner = CreateDestination();
		AttachmentNode middle = new() { Value = 20 };
		AttachmentNode root = new() { Value = 30, Child = middle };
		middle.Children.Add(owner);
		AssertRejectedWithoutMutation(root, owner, root, operation);
	}

	[TestCase("Property")]
	[TestCase("ListAdd")]
	[TestCase("ListInsert")]
	[TestCase("ListSet")]
	[TestCase("DictionaryAdd")]
	[TestCase("DictionarySet")]
	[TestCase("DictionaryNewKey")]
	public void ConflictingParentAttachmentRejectsWithoutMutation(string operation) {
		AttachmentNode owner = CreateDestination();
		AttachmentNode candidate = new() { Value = 40 };
		AttachmentNode otherParent = new() { Value = 50 };
		otherParent.ChildrenByKey.Add(5, candidate);
		AssertRejectedWithoutMutation(owner, owner, candidate, operation, otherParent);
	}

	[TestCase("Property")]
	[TestCase("ListAdd")]
	[TestCase("ListInsert")]
	[TestCase("ListSet")]
	[TestCase("DictionaryAdd")]
	[TestCase("DictionarySet")]
	[TestCase("DictionaryNewKey")]
	public void DifferentSlotOnSameOwnerRejectsWithoutMutation(string operation) {
		AttachmentNode owner = CreateDestination();
		AttachmentNode candidate = new() { Value = 40 };
		owner.OtherChild = candidate;
		AssertRejectedWithoutMutation(owner, owner, candidate, operation);
	}

	[TestCase("ListAdd")]
	[TestCase("ListInsert")]
	[TestCase("DictionaryAdd")]
	[TestCase("DictionaryNewKey")]
	public void SameChildCannotOccupyTwoEntriesInOneCollection(string operation) {
		AttachmentNode owner = CreateDestination();
		AttachmentNode candidate = operation.StartsWith("List", StringComparison.Ordinal)
			? owner.Children[0]!
			: owner.ChildrenByKey[1]!;
		AssertRejectedWithoutMutation(owner, owner, candidate, operation);
	}

	[TestCase("Property")]
	[TestCase("ListSet")]
	[TestCase("DictionarySet")]
	public void ReassigningSameInstanceIsANoOp(string operation) {
		AttachmentNode owner = CreateDestination();
		AttachmentNode candidate = operation switch {
			"Property" => owner.Child!,
			"ListSet" => owner.Children[0]!,
			_ => owner.ChildrenByKey[1]!
		};
		TreeSnapshot before = new(owner);
		Attach(owner, candidate, operation);
		before.AssertUnchanged();
	}

	[TestCase("Property", "ListAdd")]
	[TestCase("ListSet", "DictionaryAdd")]
	[TestCase("DictionarySet", "Property")]
	public void DetachedChildCanMoveAndStillPropagatesDirtyState(string oldOperation, string newOperation) {
		AttachmentNode firstOwner = CreateDestination();
		AttachmentNode child = new() { Value = 40 };
		Attach(firstOwner, child, oldOperation);
		switch (oldOperation) {
			case "Property": firstOwner.Child = null; break;
			case "ListSet": firstOwner.Children.RemoveAt(0); break;
			case "DictionarySet": firstOwner.ChildrenByKey.Remove(1); break;
		}

		Assert.Multiple(() => {
			Assert.That(((INetworkObject)child).Parent, Is.Null);
			Assert.That(((INetworkObject)child).PropertyIndex, Is.EqualTo(-1));
			Assert.That(((INetworkObject)child).IsCollectionItem, Is.False);
		});

		AttachmentNode secondOwner = CreateDestination();
		AttachmentNode root = new() { Value = 50 };
		root.ChildrenByKey.Add(10, secondOwner);
		Attach(secondOwner, child, newOperation);
		ClearDirtyState(firstOwner);
		ClearDirtyState(root);
		int changed = 0;
		child.ValueChanged += (_, _) => changed++;
		child.Value++;

		Assert.Multiple(() => {
			Assert.That(((INetworkObject)child).Parent, Is.SameAs(secondOwner));
			Assert.That(((INetworkObject)child).IsCollectionItem, Is.EqualTo(newOperation != "Property"));
			Assert.That(((INetworkObject)secondOwner).PropertyStates[((INetworkObject)child).PropertyIndex], Is.EqualTo(NetworkPropertyState.Modified));
			Assert.That(((INetworkObject)root).PropertyStates[((INetworkObject)secondOwner).PropertyIndex], Is.EqualTo(NetworkPropertyState.Modified));
			Assert.That(((INetworkObject)firstOwner).PropertyStates, Is.All.EqualTo(NetworkPropertyState.Unchanged));
			Assert.That(changed, Is.EqualTo(1));
			Assert.That(child.Anchor, Is.Null);
		});
	}

	[TestCase("Property", false)]
	[TestCase("ListAdd", false)]
	[TestCase("DictionaryAdd", false)]
	[TestCase("Property", true)]
	[TestCase("ListAdd", true)]
	[TestCase("DictionaryAdd", true)]
	public void AlreadyCyclicOwnerChainRejectsWithoutMutation(string operation, bool nullValue) {
		AttachmentNode owner = CreateDestination();
		AttachmentNode firstAncestor = new() { Value = 20 };
		AttachmentNode secondAncestor = new() { Value = 30 };
		AttachmentNode? candidate = nullValue ? null : new() { Value = 40 };
		((INetworkObject)owner).Parent = firstAncestor;
		((INetworkObject)owner).PropertyIndex = 0;
		((INetworkObject)firstAncestor).Parent = secondAncestor;
		((INetworkObject)firstAncestor).PropertyIndex = 0;
		((INetworkObject)secondAncestor).Parent = firstAncestor;
		((INetworkObject)secondAncestor).PropertyIndex = 0;

		try {
			// Snapshot follows member values, not the deliberately corrupted parent links.
			AssertRejectedWithoutMutation(owner, owner, candidate, operation, firstAncestor, secondAncestor);
		} finally {
			((INetworkObject)owner).Parent = null;
			((INetworkObject)firstAncestor).Parent = null;
			((INetworkObject)secondAncestor).Parent = null;
		}
	}

	[TestCase(false)]
	[TestCase(true)]
	public void DeserializedReplacementValidatesBeforeDetachingPreviousValue(bool dictionary) {
		AttachmentNode owner = CreateDestination();
		TreeSnapshot before = new(owner);
		object collection = dictionary ? owner.ChildrenByKey : owner.Children;
		Type collectionType = dictionary ? typeof(NetworkDictionary<int, AttachmentNode?>) : typeof(NetworkList<AttachmentNode?>);
		MethodInfo set = collectionType.GetMethod("SetDeserialized", BindingFlags.Instance | BindingFlags.NonPublic)!;

		// Exercise the shared replacement hook directly with an invalid decoded object.
		TargetInvocationException? error = Assert.Throws<TargetInvocationException>(() => set.Invoke(collection, [dictionary ? 1 : 0, owner]));
		Assert.That(error!.InnerException, Is.TypeOf<InvalidOperationException>());
		before.AssertUnchanged();
	}

	private static AttachmentNode CreateDestination() {
		AttachmentNode owner = new() { Value = 10, Child = new AttachmentNode { Value = 11 } };
		owner.Children.Add(new AttachmentNode { Value = 12 });
		owner.ChildrenByKey.Add(1, new AttachmentNode { Value = 13 });
		return owner;
	}

	private static void Attach(AttachmentNode owner, AttachmentNode? candidate, string operation) {
		switch (operation) {
			case "Property": owner.Child = candidate; break;
			case "ListAdd": owner.Children.Add(candidate); break;
			case "ListInsert": owner.Children.Insert(0, candidate); break;
			case "ListSet": owner.Children[0] = candidate; break;
			case "DictionaryAdd": owner.ChildrenByKey.Add(2, candidate); break;
			case "DictionarySet": owner.ChildrenByKey[1] = candidate; break;
			case "DictionaryNewKey": owner.ChildrenByKey[2] = candidate; break;
			default: throw new ArgumentException("Unknown attachment operation.", nameof(operation));
		}
	}

	private static void AssertRejectedWithoutMutation(AttachmentNode root, AttachmentNode owner, AttachmentNode? candidate, string operation, params AttachmentNode[] otherRoots) {
		TreeSnapshot before = new([root, .. otherRoots, candidate ?? root]);
		Assert.That(() => Attach(owner, candidate, operation), Throws.TypeOf<InvalidOperationException>());
		before.AssertUnchanged();
	}

	private static (INetworkObjectSerializer Serializer, SerializationContext Context) GetSerializer() {
		TypeCatalogue catalogue = new();
		catalogue.Register(typeof(AttachmentNode));
		catalogue.TryFindSerializer(typeof(AttachmentNode), out INetworkObjectSerializer? serializer);
		return (serializer!, new SerializationContext(catalogue));
	}

	private static byte[] Serialize(AttachmentNode node, MemberSelectionMode mode) {
		(INetworkObjectSerializer serializer, SerializationContext context) = GetSerializer();
		BufferWriter writer = new();
		serializer.Serialize(writer, node, context, new SerializationOptions(mode, MemberIdentificationMode.Index));
		return writer.GetWrittenSpan().ToArray();
	}

	private static void ClearDirtyState(AttachmentNode root) {
		(INetworkObjectSerializer serializer, SerializationContext context) = GetSerializer();
		serializer.ClearDirtyState(root, context);
	}

	private sealed class TreeSnapshot {
		private readonly List<Action> checks = [];
		private int events;

		public TreeSnapshot(params AttachmentNode[] roots) {
			HashSet<AttachmentNode> seen = new(ReferenceEqualityComparer.Instance);
			foreach (AttachmentNode root in roots) {
				Capture(root, seen);
			}
		}

		public void AssertUnchanged() {
			Assert.Multiple(() => {
				foreach (Action check in checks) {
					check();
				}
				Assert.That(events, Is.Zero, "Rejected or identical assignments must not raise events.");
			});
		}

		private void Capture(AttachmentNode node, HashSet<AttachmentNode> seen) {
			if (!seen.Add(node)) {
				return;
			}

			INetworkObject state = node;
			NetworkObject? parent = state.Parent;
			int propertyIndex = state.PropertyIndex;
			bool collectionItem = state.IsCollectionItem;
			NetworkPropertyState[] propertyStates = state.PropertyStates.ToArray();
			AttachmentNode? child = node.Child;
			AttachmentNode? otherChild = node.OtherChild;
			AttachmentNode?[] list = node.Children.ToArray();
			KeyValuePair<int, AttachmentNode?>[] dictionary = node.ChildrenByKey.ToArray();
			byte[] fullPayload = Serialize(node, MemberSelectionMode.All);
			byte[] dirtyPayload = Serialize(node, MemberSelectionMode.Dirty);
			node.PropertyChanged += (_, _) => events++;
			node.ChildChanged += (_, _) => events++;
			node.OtherChildChanged += (_, _) => events++;
			node.Children.ItemAdded += (_, _) => events++;
			node.Children.ItemRemoved += (_, _) => events++;
			node.Children.IndexChanged += (_, _) => events++;
			node.ChildrenByKey.ItemAdded += (_, _) => events++;
			node.ChildrenByKey.ItemRemoved += (_, _) => events++;
			node.ChildrenByKey.ValueChanged += (_, _) => events++;

			checks.Add(() => {
				Assert.That(state.Parent, Is.SameAs(parent));
				Assert.That(state.PropertyIndex, Is.EqualTo(propertyIndex));
				Assert.That(state.IsCollectionItem, Is.EqualTo(collectionItem));
				Assert.That(state.PropertyStates, Is.EqualTo(propertyStates));
				Assert.That(node.Child, Is.SameAs(child));
				Assert.That(node.OtherChild, Is.SameAs(otherChild));
				Assert.That(node.Children.Count, Is.EqualTo(list.Length));
				for (int index = 0; index < Math.Min(node.Children.Count, list.Length); index++) {
					Assert.That(node.Children[index], Is.SameAs(list[index]));
				}
				Assert.That(node.ChildrenByKey.Keys, Is.EquivalentTo(dictionary.Select(entry => entry.Key)));
				foreach ((int key, AttachmentNode? value) in dictionary) {
					Assert.That(node.ChildrenByKey[key], Is.SameAs(value));
				}
				Assert.That(Serialize(node, MemberSelectionMode.All), Is.EqualTo(fullPayload));
				Assert.That(Serialize(node, MemberSelectionMode.Dirty), Is.EqualTo(dirtyPayload));
			});

			foreach (AttachmentNode? descendant in new[] { child, otherChild }.Concat(list).Concat(dictionary.Select(entry => entry.Value))) {
				if (descendant is not null) {
					Capture(descendant, seen);
				}
			}
		}
	}
}
