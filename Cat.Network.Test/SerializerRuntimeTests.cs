using System.Buffers.Binary;
using System.Text;
using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	private static void Deserialize(NetworkObject target, TypeCatalogue catalogue, byte[] payload) {
		Assert.That(catalogue.TryFindSerializer(target.GetType(), out INetworkObjectSerializer? serializer), Is.True);
		SerializationContext context = new(catalogue);
		serializer!.Deserialize(target, payload, context);
		serializer.ClearDirtyState(target, context);
	}

	private static void Pump(RelayServer server, params RelayClient[] clients) {
		for (int i = 0; i < 4; i++) {
			server.Tick();
			foreach (RelayClient client in clients) {
				client.Tick();
			}
		}
	}

	private static void AssertRoundTripSerializationEquals<T>(T original, T roundTripTarget, TypeCatalogue catalogue)
		where T : NetworkObject {
		byte[] firstPayload = Serialize(original, catalogue);
		Deserialize(roundTripTarget, catalogue, firstPayload);
		byte[] secondPayload = Serialize(roundTripTarget, catalogue);

		Assert.That(secondPayload, Is.EqualTo(firstPayload));
	}

	private static byte[] Serialize(
		NetworkObject target,
		TypeCatalogue catalogue,
		MemberIdentificationMode memberIdentificationMode = MemberIdentificationMode.Index,
		MemberSelectionMode memberSelectionMode = MemberSelectionMode.All) {
		Assert.That(catalogue.TryFindSerializer(target.GetType(), out INetworkObjectSerializer? serializer), Is.True);
		BufferWriter writer = new();
		serializer!.Serialize(writer, target, new SerializationContext(catalogue), new SerializationOptions(memberSelectionMode, memberIdentificationMode));
		return writer.GetWrittenSpan().ToArray();
	}

	private static TypeCatalogue RegisterTypes(params Type[] types) {
		TypeCatalogue catalogue = new();
		foreach (Type type in types) {
			catalogue.Register(type);
		}

		return catalogue;
	}

	private static Guid GetTypeId(Type type) {
		return type.GetCustomAttributes(typeof(NetworkObjectTypeId), inherit: false)
			.OfType<NetworkObjectTypeId>()
			.Single()
			.Id;
	}

	private static byte[] BuildObjectPayload(params byte[][] fields) {
		return BuildObjectPayload(0, MemberIdentificationMode.Index, fields);
	}

	private static byte[] BuildObjectPayload(MemberIdentificationMode memberIdentificationMode, params byte[][] fields) {
		return BuildObjectPayload(0, memberIdentificationMode, fields);
	}

	private static byte[] BuildObjectPayload(ushort version, MemberIdentificationMode memberIdentificationMode, params byte[][] fields) {
		return Concat(
			UInt16(version),
			new[] { (byte)memberIdentificationMode },
			UInt16((ushort)fields.Length),
			Concat(fields));
	}

	private static byte[] BuildIndexField(ushort index, byte[] value) {
		return Concat(UInt16(index), UInt32((uint)value.Length), value);
	}

	private static byte[] BuildNameField(string name, byte[] value) {
		byte[] nameBytes = Utf8(name);
		return Concat(UInt32((uint)nameBytes.Length), nameBytes, UInt32((uint)value.Length), value);
	}

	private static byte[] BuildStatsPayload(float accuracy, double criticalChance, double? ultraVision, int health, string? name, Guid? sessionId) {
		return Concat(
			Single(accuracy),
			Double(criticalChance),
			ultraVision.HasValue
				? NullableStructValue(Double(ultraVision.Value))
				: NullValue(),
			Int32(health),
			LengthPrefixedStringValue(name),
			sessionId.HasValue
				? NullableValue(GuidBytes(sessionId.Value))
				: NullValue());
	}

	private static byte[] ModifyObject(byte[] objectData) {
		return Concat(new[] { (byte)NetworkObjectUpdateMode.Modify }, objectData);
	}

	private static byte[] ReplaceObject(Guid typeId, byte[] objectData) {
		return Concat(new[] { (byte)NetworkObjectUpdateMode.Replace }, GuidBytes(typeId), objectData);
	}

	private static byte[] ClearObject() {
		return new[] { (byte)NetworkObjectUpdateMode.Clear };
	}

	private static byte[] CollectionPayload(params byte[][] operations) {
		return Concat(Int32(operations.Length), Concat(operations));
	}

	private static byte[] CollectionAdd(byte[] itemPayload) {
		return Concat(new[] { (byte)NetworkCollectionOperationType.Add }, Int32(itemPayload.Length), itemPayload);
	}

	private static byte[] CollectionInsert(int index, byte[] itemPayload) {
		return Concat(new[] { (byte)NetworkCollectionOperationType.Insert }, Int32(index), Int32(itemPayload.Length), itemPayload);
	}

	private static byte[] CollectionRemove(int index) {
		return Concat(new[] { (byte)NetworkCollectionOperationType.Remove }, Int32(index));
	}

	private static byte[] CollectionSet(int index, byte[] itemPayload) {
		return Concat(new[] { (byte)NetworkCollectionOperationType.Set }, Int32(index), Int32(itemPayload.Length), itemPayload);
	}

	private static byte[] CollectionClear() {
		return new[] { (byte)NetworkCollectionOperationType.Clear };
	}

	private static byte[] CollectionUpdate(int index, byte[] itemPayload) {
		return Concat(new[] { (byte)NetworkCollectionOperationType.Update }, Int32(index), Int32(itemPayload.Length), itemPayload);
	}

	private static byte[] CollectionDictionaryAdd(byte[] keyPayload, byte[] valuePayload) {
		return Concat(new[] { (byte)NetworkCollectionOperationType.Add }, Int32(keyPayload.Length), keyPayload, Int32(valuePayload.Length), valuePayload);
	}

	private static byte[] CollectionDictionaryRemove(byte[] keyPayload) {
		return Concat(new[] { (byte)NetworkCollectionOperationType.Remove }, Int32(keyPayload.Length), keyPayload);
	}

	private static byte[] CollectionDictionarySet(byte[] keyPayload, byte[] valuePayload) {
		return Concat(new[] { (byte)NetworkCollectionOperationType.Set }, Int32(keyPayload.Length), keyPayload, Int32(valuePayload.Length), valuePayload);
	}

	private static byte[] CollectionDictionaryUpdate(byte[] keyPayload, byte[] valuePayload) {
		return Concat(new[] { (byte)NetworkCollectionOperationType.Update }, Int32(keyPayload.Length), keyPayload, Int32(valuePayload.Length), valuePayload);
	}

	private static byte[] ObjectItem(Type type, byte[] objectPayload) {
		return Concat(new byte[] { 1 }, GuidBytes(GetTypeId(type)), objectPayload);
	}

	private static byte[] NullableValue(byte[] value) {
		return Concat(new byte[] { 1 }, value);
	}

	private static byte[] StringValue(string? value) {
		return value is null
			? NullValue()
			: Concat(new byte[] { 1 }, Utf8(value));
	}

	private static byte[] LengthPrefixedStringValue(string? value) {
		return value is null
			? NullValue()
			: NullableValue(LengthPrefixedUtf8(value));
	}

	private static byte[] NullableStructValue(byte[] structBytes) {
		return Concat(new byte[] { 1 }, structBytes);
	}

	private static byte[] NullValue() {
		return new byte[] { 0 };
	}

	private static byte[] LengthPrefixedUtf8(string value) {
		byte[] utf8 = Utf8(value);
		return Concat(UInt32((uint)utf8.Length), utf8);
	}

	private static byte[] Utf8(string value) {
		return Encoding.UTF8.GetBytes(value);
	}

	private static byte[] GuidBytes(Guid value) {
		return value.ToByteArray();
	}

	private static bool ContainsGuid(byte[] message, Guid value) {
		byte[] guid = GuidBytes(value);
		for (int i = 0; i <= message.Length - guid.Length; i++) {
			if (message.AsSpan(i, guid.Length).SequenceEqual(guid)) {
				return true;
			}
		}

		return false;
	}

	private static byte[] UInt16(ushort value) {
		byte[] bytes = new byte[2];
		BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
		return bytes;
	}

	private static byte[] UInt32(uint value) {
		byte[] bytes = new byte[4];
		BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
		return bytes;
	}

	private static byte[] Int32(int value) {
		byte[] bytes = new byte[4];
		BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
		return bytes;
	}

	private static byte[] Single(float value) {
		byte[] bytes = new byte[4];
		BinaryPrimitives.WriteSingleLittleEndian(bytes, value);
		return bytes;
	}

	private static byte[] Double(double value) {
		byte[] bytes = new byte[8];
		BinaryPrimitives.WriteDoubleLittleEndian(bytes, value);
		return bytes;
	}

	private static byte[] Concat(params byte[][] arrays) {
		int totalLength = arrays.Sum(static array => array.Length);
		byte[] result = new byte[totalLength];
		int offset = 0;

		foreach (byte[] array in arrays) {
			Buffer.BlockCopy(array, 0, result, offset, array.Length);
			offset += array.Length;
		}

		return result;
	}

	private sealed class RelevantEntityStorage : TestEntityStorage {
		private HashSet<(Guid ProfileId, Guid EntityId)> RelevantEntities { get; } = [];

		public void Allow(Guid profileId, Guid entityId) {
			RelevantEntities.Add((profileId, entityId));
		}

		public void Deny(Guid profileId, Guid entityId) {
			RelevantEntities.Remove((profileId, entityId));
		}

		public override void PopulateRelevantEntities(NetworkProfile profile, ICollection<NetworkEntity> entities) {
			foreach ((Guid profileId, Guid entityId) in RelevantEntities) {
				if (profileId == profile.Id && TryGetEntity(entityId, out NetworkEntity? entity)) {
					entities.Add(entity);
				}
			}
		}
	}
}
