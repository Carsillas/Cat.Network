using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed partial class SerializerRuntimeTests {
	private static IEnumerable<TestCaseData> TupleCases() {
		yield return new(nameof(TupleWireState.Unnamed), TupleInts(42, 17));
		yield return new(nameof(TupleWireState.Named), TupleInts(42, 17));
		yield return new(nameof(TupleWireState.DefaultNames), TupleInts(42, 17));
		yield return new(nameof(TupleWireState.MixedNames), TupleInts(42, 17, 9));
		yield return new(nameof(TupleWireState.Nullable), Concat(new byte[] { 1 }, TupleInts(42, 17)));
		yield return new(nameof(TupleWireState.Null), new byte[] { 0 });
		yield return new(nameof(TupleWireState.Nested), TupleInts(42, 17, 9));
		yield return new(nameof(TupleWireState.NestedNullable), Concat(new byte[] { 1 }, Int32(42), new byte[] { 1 }, TupleInts(17, 9)));
		yield return new(nameof(TupleWireState.NestedNull), Concat(Int32(42), new byte[] { 0 }));
		yield return new(nameof(TupleWireState.Eight), TupleInts(1, 2, 3, 4, 5, 6, 7, 8));
		yield return new(nameof(TupleWireState.Nine), TupleInts(1, 2, 3, 4, 5, 6, 7, 8, 9));
		yield return new(nameof(TupleWireState.LongMixedNames), TupleInts(1, 2, 3, 4, 5, 6, 7, 8, 9));
		yield return new(nameof(TupleWireState.ExplicitStorage), TupleInts(1, 2, 3, 4, 5, 6, 7, 8, 9));
		yield return new(nameof(TupleWireState.Fifteen), TupleInts(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15));
		yield return new(nameof(TupleWireState.NullableInRest), Concat(TupleInts(1, 2, 3, 4, 5, 6, 7), new byte[] { 1 }, TupleInts(8, 9)));
		yield return new(nameof(TupleWireState.Wrapped), TupleInts(42, 17, 1, 2, 3, 4, 5, 6, 7, 8, 9));
	}

	[TestCaseSource(nameof(TupleCases))]
	public void TuplePropertyPayloadMatchesPhysicalValueTupleFields(string propertyName, byte[] expected) {
		TypeCatalogue catalogue = RegisterTypes(typeof(TupleWireState));
		NetworkObjectUpgradeReader reader = TupleReader(Serialize(new TupleWireState(), catalogue, MemberIdentificationMode.Name), catalogue);
		Assert.That(reader.Fields.Single(field => field.Name == propertyName).Value.ToArray(), Is.EqualTo(expected));
	}

	[TestCaseSource(nameof(TupleCases))]
	public void GeneratedTupleCanBeReadByUpgradeCodec(string propertyName, byte[] expected) {
		TypeCatalogue catalogue = RegisterTypes(typeof(TupleWireState));
		TupleWireState source = new();
		PropertyInfo property = typeof(TupleWireState).GetProperty(propertyName)!;
		NetworkObjectUpgradeReader reader = TupleReader(Serialize(source, catalogue, MemberIdentificationMode.Name), catalogue);
		object? decoded = InvokeTupleGeneric(typeof(NetworkObjectUpgradeReader).GetMethod(nameof(NetworkObjectUpgradeReader.Get))!, property.PropertyType, reader, propertyName);
		Assert.That(decoded, Is.EqualTo(property.GetValue(source)));
	}

	[TestCaseSource(nameof(TupleCases))]
	public void UpgradeTuplePayloadMatchesGoldenBytesAndAppliesToGeneratedProperty(string propertyName, byte[] expected) {
		TypeCatalogue catalogue = RegisterTypes(typeof(TupleWireState));
		PropertyInfo property = typeof(TupleWireState).GetProperty(propertyName)!;
		object? value = property.GetValue(new TupleWireState());
		BufferWriter buffer = new();
		NetworkObjectUpgradeWriter writer = new(buffer, null, new(catalogue));
		InvokeTupleGeneric(typeof(NetworkObjectUpgradeWriter).GetMethod(nameof(NetworkObjectUpgradeWriter.Write))!, property.PropertyType, writer, propertyName, value);
		byte[] payload = Concat(UInt16(0), writer.Complete());
		Assert.That(TupleReader(payload, catalogue).Fields.Single().Value.ToArray(), Is.EqualTo(expected));

		TupleWireState target = new();
		// Start with a different value so silently ignoring a field cannot pass a round trip.
		property.SetValue(target, Activator.CreateInstance(Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType));
		Deserialize(target, catalogue, payload);
		Assert.That(property.GetValue(target), Is.EqualTo(value));
	}

	[TestCaseSource(nameof(TupleCases))]
	public void TupleListItemsMatchPropertyPayloadAndRoundTrip(string propertyName, byte[] expected) {
		TypeCatalogue catalogue = RegisterTypes(typeof(TupleWireState));
		TupleWireState source = new();
		PropertyInfo property = typeof(TupleWireState).GetProperty(propertyName)!;
		byte[] generated = TupleReader(Serialize(source, catalogue, MemberIdentificationMode.Name), catalogue).Fields.Single(field => field.Name == propertyName).Value.ToArray();
		InvokeTupleGeneric(typeof(SerializerRuntimeTests).GetMethod(nameof(CheckTupleList), BindingFlags.Static | BindingFlags.NonPublic)!, property.PropertyType, null, property.GetValue(source), expected, generated, catalogue);
	}

	private static void CheckTupleList<T>(object? value, byte[] expected, byte[] generated, TypeCatalogue catalogue) {
		NetworkValueList<T> list = new();
		((INetworkCollection)list).Initialize(new TupleWireState(), 0);
		list.Add((T)value!);
		BufferWriter buffer = new();
		((INetworkCollection)list).Serialize(buffer, new(catalogue), new(MemberSelectionMode.All, MemberIdentificationMode.Name));
		Assert.That(buffer.GetWrittenSpan().ToArray(), Is.EqualTo(CollectionPayload(CollectionClear(), CollectionAdd(expected))));

		NetworkValueList<T> target = new();
		((INetworkCollection)target).Initialize(new TupleWireState(), 0);
		((INetworkCollection)target).Deserialize(CollectionPayload(CollectionClear(), CollectionAdd(generated)), new(catalogue));
		Assert.That(target.Single(), Is.EqualTo((T)value!));
	}

	[Test]
	public void GeneratedTupleCollectionsUseTheSamePhysicalItemAndKeyEncoding() {
		TypeCatalogue catalogue = RegisterTypes(typeof(TupleWireState));
		TupleWireState source = new();
		source.NestedItems.Add(source.NestedNullable);
		source.NestedItems.Add(null);
		source.Lookup.Add(source.Named, source.Nine);
		byte[] payload = Serialize(source, catalogue, MemberIdentificationMode.Name);
		NetworkObjectUpgradeReader reader = TupleReader(payload, catalogue);
		byte[] nestedBytes = Concat(new byte[] { 1 }, Int32(42), new byte[] { 1 }, TupleInts(17, 9));
		Assert.Multiple(() => {
			Assert.That(reader.Fields.Single(field => field.Name == nameof(TupleWireState.NestedItems)).Value.ToArray(),
				Is.EqualTo(CollectionPayload(CollectionClear(), CollectionAdd(nestedBytes), CollectionAdd(new byte[] { 0 }))));
			Assert.That(reader.Fields.Single(field => field.Name == nameof(TupleWireState.Lookup)).Value.ToArray(),
				Is.EqualTo(CollectionPayload(CollectionClear(), CollectionDictionaryAdd(TupleInts(42, 17), TupleInts(1, 2, 3, 4, 5, 6, 7, 8, 9)))));
		});
		TupleWireState target = new();
		Deserialize(target, catalogue, payload);
		Assert.Multiple(() => {
			Assert.That(target.NestedItems, Is.EqualTo(source.NestedItems));
			Assert.That(target.Lookup[source.Named], Is.EqualTo(source.Nine));
		});
	}

	[TestCase(0, 1)]
	[TestCase(1, 2)]
	[TestCase(0, 2)]
	public void TupleAliasChangesMigrateThroughRealSchemaVersions(int fromVersion, int toVersion) {
		TypeCatalogue catalogue = RegisterTypes(typeof(TupleVersion0), typeof(TupleVersion1), typeof(TupleVersion2));
		NetworkObject source = fromVersion == 0 ? new TupleVersion0() : new TupleVersion1();
		NetworkObject target = toVersion == 1 ? new TupleVersion1() : new TupleVersion2();
		source.GetType().GetProperty("Pair")!.SetValue(source, (42, 17));
		source.GetType().GetProperty("Long")!.SetValue(source, (1, 2, 3, 4, 5, 6, 7, 8, 9));
		target.GetType().GetProperty("Pair")!.SetValue(target, (5, 6));
		Deserialize(target, catalogue, Serialize(source, catalogue, MemberIdentificationMode.Name));
		Assert.Multiple(() => {
			Assert.That(target.GetType().GetProperty("Pair")!.GetValue(target), Is.EqualTo((42, 17)));
			Assert.That(target.GetType().GetProperty("Long")!.GetValue(target), Is.EqualTo((1, 2, 3, 4, 5, 6, 7, 8, 9)));
		});
	}

	[Test]
	public void LegacyDuplicateNamedTuplePayloadRequiresExplicitConversionBeforeTypedUpgradeReading() {
		TypeCatalogue catalogue = RegisterTypes(typeof(TupleWireState));
		// The old (int Z, int A) generator emitted A, Item1, Item2, Z.
		byte[] legacy = BuildObjectPayload(MemberIdentificationMode.Name, BuildNameField(nameof(TupleWireState.Named), TupleInts(17, 42, 17, 42)));
		Assert.That(() => TupleReader(legacy, catalogue).Get<(int, int)>(nameof(TupleWireState.Named)), Throws.InvalidOperationException);
	}

	[TestCase(false, false)]
	[TestCase(false, true)]
	[TestCase(true, false)]
	[TestCase(true, true)]
	public void TupleMessagesWriteCanonicalParameterBytesAndPreserveMessageIds(bool broadcast, bool nullOptional) {
		TypeCatalogue catalogue = RegisterTypes(typeof(TupleMessageState), typeof(RelayProfileState));
		RecordingRelayTransport transport = new();
		RelayClient client = new(catalogue);
		client.Connect(transport);
		Guid profileId = Guid.NewGuid();
		transport.Receive(BuildProfileMessage(catalogue, profileId, new RelayProfileState()));
		transport.Receive(BuildAssignProfileMessage(profileId));
		TupleMessageState sender;
		if (broadcast) {
			sender = new();
			client.Spawn(sender);
		} else {
			Guid entityId = Guid.NewGuid();
			transport.Receive(BuildCreateEntityMessage(catalogue, entityId, new TupleMessageState()));
			Assert.That(client.TryGetEntity(entityId, out NetworkEntity? entity), Is.True);
			sender = (TupleMessageState)entity!;
		}
		TupleWireState values = new();
		if (broadcast) sender.PublishTuples(values.Named, values.Nine, values.Wrapped, nullOptional ? null : values.NestedNullable);
		else sender.ApplyTuples(values.Named, values.Nine, values.Wrapped, nullOptional ? null : values.NestedNullable);
		client.Tick();
		EntityMessageKind kind = broadcast ? EntityMessageKind.Broadcast : EntityMessageKind.Rpc;
		byte[] message = transport.SentMessages.Single(bytes => bytes[0] == (byte)NetworkMessageChannel.EntityMessage && bytes[1] == (byte)kind);
		byte[] expected = CanonicalTupleMessageParameters(nullOptional);
		Assert.Multiple(() => {
			Assert.That(BinaryPrimitives.ReadInt32LittleEndian(message.AsSpan(18)), Is.EqualTo(sizeof(ulong) + expected.Length));
			Assert.That(BinaryPrimitives.ReadUInt64LittleEndian(message.AsSpan(22)), Is.EqualTo(TupleMessageId(broadcast)));
			Assert.That(message[30..], Is.EqualTo(expected));
		});
	}

	[TestCase(false, false)]
	[TestCase(false, true)]
	[TestCase(true, false)]
	[TestCase(true, true)]
	public void TupleMessagesAcceptCanonicalParameterBytes(bool broadcast, bool nullOptional) {
		TypeCatalogue catalogue = RegisterTypes(typeof(TupleMessageState), typeof(RelayProfileState));
		TupleMessageState target = new();
		object?[] received = [];
		int calls = 0;
		if (broadcast) {
			target.PublishTuplesReceived += (_, _, pair, longValue, wrapped, optional) => {
				received = [pair, longValue, wrapped, optional];
				calls++;
			};
		} else {
			target.ApplyTuplesReceived += (_, _, pair, longValue, wrapped, optional) => {
				received = [pair, longValue, wrapped, optional];
				calls++;
			};
		}
		byte[] parameters = CanonicalTupleMessageParameters(nullOptional);
		INetworkRpcTarget dispatcher = target;
		bool accepted = broadcast
			? dispatcher.TryInvokeBroadcast(new(catalogue), new RelayProfileState(), TupleMessageId(true), parameters, new(catalogue))
			: dispatcher.TryInvokeRpc(new(catalogue), new RelayProfileState(), TupleMessageId(false), parameters, new(catalogue));
		TupleWireState values = new();
		Assert.Multiple(() => {
			Assert.That(accepted, Is.True);
			Assert.That(calls, Is.EqualTo(1));
			Assert.That(received, Is.EqualTo(new object?[] { values.Named, values.Nine, values.Wrapped, nullOptional ? null : values.NestedNullable }));
		});
	}

	private static byte[] CanonicalTupleMessageParameters(bool nullOptional) {
		byte[][] values = [
			TupleInts(42, 17),
			TupleInts(1, 2, 3, 4, 5, 6, 7, 8, 9),
			TupleInts(42, 17, 1, 2, 3, 4, 5, 6, 7, 8, 9),
			nullOptional ? new byte[] { 0 } : Concat(new byte[] { 1 }, Int32(42), new byte[] { 1 }, TupleInts(17, 9))
		];
		return Concat(values.Select(value => Concat(Int32(value.Length), value)).ToArray());
	}

	private static ulong TupleMessageId(bool broadcast) {
		// Keep the existing alias-sensitive signature contract separate from physical payload encoding.
		string method = broadcast ? nameof(TupleMessageState.PublishTuples) : nameof(TupleMessageState.ApplyTuples);
		string signature = method + "(" + string.Join(",", new[] {
			"(global::System.Int32 Z, global::System.Int32 A)",
			"(global::System.Int32 Z, global::System.Int32 Y, global::System.Int32 X, global::System.Int32 W, global::System.Int32 V, global::System.Int32 U, global::System.Int32 T, global::System.Int32 S, global::System.Int32 R)",
			"global::Cat.Network.Test.Entities.TupleEnvelope",
			"(global::System.Int32 Z, (global::System.Int32 Y, global::System.Int32 X)? Nested)?"
		}) + ")";
		return BinaryPrimitives.ReadUInt64LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(signature)));
	}

	private static NetworkObjectUpgradeReader TupleReader(byte[] payload, TypeCatalogue catalogue) {
		Assert.That(NetworkObjectUpgradeReader.TryCreate(payload.AsSpan(2), new(catalogue), out NetworkObjectUpgradeReader reader), Is.True);
		return reader;
	}

	private static byte[] TupleInts(params int[] values) => Concat(values.Select(Int32).ToArray());

	private static object? InvokeTupleGeneric(MethodInfo method, Type type, object? target, params object?[] arguments) {
		try {
			return method.MakeGenericMethod(type).Invoke(target, arguments);
		} catch (TargetInvocationException exception) when (exception.InnerException is not null) {
			ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
			throw;
		}
	}
}
