using System.Reflection;
using Cat.Network.Test.Entities;

namespace Cat.Network.Test;

public sealed class StructFallbackRuntimeTests {
	private static IEnumerable<Type> UnsupportedTypes => [
		typeof(decimal), typeof(DateTime), typeof(char), typeof(TimeSpan),
		typeof(decimal?), typeof(DateTime?), typeof(char?),
		typeof(FallbackHidden), typeof(FallbackAutoProperty),
		typeof(FallbackNested<decimal>), typeof(FallbackNested<FallbackNested<DateTime>>), typeof(FallbackNested<char?>)
	];

	[TestCaseSource(nameof(UnsupportedTypes))]
	public void DirectListUseRejectsUnsupportedTypes(Type type) {
		AssertUnsupportedCollection(typeof(NetworkValueList<>).MakeGenericType(type), [Activator.CreateInstance(type)]);
	}

	[TestCaseSource(nameof(UnsupportedTypes))]
	public void DirectDictionaryValueUseRejectsUnsupportedTypes(Type type) {
		AssertUnsupportedCollection(typeof(NetworkValueDictionary<,>).MakeGenericType(typeof(int), type), [1, Activator.CreateInstance(type)]);
	}

	[TestCase(typeof(decimal))]
	[TestCase(typeof(DateTime))]
	[TestCase(typeof(char))]
	[TestCase(typeof(FallbackHidden))]
	[TestCase(typeof(FallbackNested<decimal>))]
	public void DirectDictionaryKeyUseRejectsUnsupportedTypes(Type type) {
		AssertUnsupportedCollection(typeof(NetworkValueDictionary<,>).MakeGenericType(type, typeof(int)), [Activator.CreateInstance(type), 1]);
	}

	[Test]
	public void ExplicitScalarCodecsStillRoundTrip() {
		AssertCollectionRoundTrip(true);
		AssertCollectionRoundTrip((byte)231);
		AssertCollectionRoundTrip((sbyte)-123);
		AssertCollectionRoundTrip((short)-31234);
		AssertCollectionRoundTrip((ushort)61234);
		AssertCollectionRoundTrip(-123456789);
		AssertCollectionRoundTrip(3123456789U);
		AssertCollectionRoundTrip(-123456789012345L);
		AssertCollectionRoundTrip(123456789012345UL);
		AssertCollectionRoundTrip(123.5f);
		AssertCollectionRoundTrip(456.25d);
		AssertCollectionRoundTrip("supported");
		AssertCollectionRoundTrip(new Guid("00112233-4455-6677-8899-aabbccddeeff"));
		AssertCollectionRoundTrip<int?>(42);
		AssertCollectionRoundTrip<Guid?>(Guid.Empty);
	}

	[Test]
	public void PublicFieldsAndEmptyStructsRoundTripThroughGeneratedPropertiesCollectionsAndUpgrades() {
		FallbackValue value = new() { Number = 42, Optional = 7, Empty = new(), Nested = new() { Value = 123 }, Id = Guid.NewGuid() };
		TypeCatalogue catalogue = new();
		catalogue.Register(typeof(FallbackState));
		Assert.That(catalogue.TryFindSerializer(typeof(FallbackState), out INetworkObjectSerializer? serializer), Is.True);
		SerializationContext context = new(catalogue);
		FallbackState source = new() { Value = value, OptionalEmpty = new FallbackEmpty() };
		source.Values.Add(value);
		source.EmptyValues.Add(new FallbackEmpty());
		source.EmptyKeys.Add(new FallbackEmpty(), 42);
		BufferWriter writer = new();
		serializer!.Serialize(writer, source, context, FullOptions);
		FallbackState target = new();
		serializer.Deserialize(target, writer.GetWrittenSpan(), context);
		Assert.Multiple(() => {
			Assert.That(target.Value, Is.EqualTo(value));
			Assert.That(target.Values.Single(), Is.EqualTo(value));
			Assert.That(target.OptionalEmpty.HasValue, Is.True);
			Assert.That(target.EmptyValues, Has.Count.EqualTo(1));
			Assert.That(target.EmptyKeys[new FallbackEmpty()], Is.EqualTo(42));
		});
		AssertCollectionRoundTrip(value);
		AssertCollectionRoundTrip(new FallbackEmpty());
		AssertCollectionRoundTrip(new FallbackStateless());
		AssertCollectionRoundTrip<FallbackEmpty?>(new FallbackEmpty());
		AssertUpgradeRoundTrip(value);
		AssertUpgradeRoundTrip(new FallbackEmpty());
		AssertUpgradeRoundTrip(new FallbackStateless());
		AssertUpgradeRoundTrip<FallbackEmpty?>(new FallbackEmpty());
	}

	[Test]
	public void PublicFieldContractContinuesToIgnoreAdditionalPrivateState() {
		FallbackMixed value = new(42, 99);
		NetworkValueList<FallbackMixed> source = Initialize(new NetworkValueList<FallbackMixed>());
		source.Add(value);
		NetworkValueList<FallbackMixed> target = Initialize(new NetworkValueList<FallbackMixed>());
		Transfer(source, target);
		Assert.Multiple(() => {
			Assert.That(target.Single().Value, Is.EqualTo(42));
			Assert.That(target.Single().Local, Is.Zero);
		});
	}

	[TestCase(typeof(decimal))]
	[TestCase(typeof(DateTime))]
	[TestCase(typeof(char))]
	[TestCase(typeof(FallbackHidden))]
	[TestCase(typeof(FallbackNested<decimal>))]
	public void UpgradesRejectHiddenStateTypes(Type type) {
		NetworkObjectUpgradeWriter writer = new(new BufferWriter(), null, new SerializationContext(new TypeCatalogue()));
		Exception? exception = Assert.Catch(() => typeof(NetworkObjectUpgradeWriter).GetMethod(nameof(NetworkObjectUpgradeWriter.Write))!
			.MakeGenericMethod(type).Invoke(writer, ["Value", Activator.CreateInstance(type)]));
		Assert.That(exception!.GetBaseException(), Is.TypeOf<InvalidOperationException>().With.Message.Contains("not supported"));
	}

	[TestCase(typeof(decimal?))]
	[TestCase(typeof(DateTime?))]
	[TestCase(typeof(char?))]
	public void UpgradesRejectUnsupportedNullableTypesEvenWhenValueIsNull(Type type) {
		UpgradesRejectHiddenStateTypes(type);
		SerializationContext context = new(new TypeCatalogue());
		NetworkObjectUpgradeWriter writer = new(new BufferWriter(), null, context);
		writer.Write<int?>("Value", null);
		Assert.That(NetworkObjectUpgradeReader.TryCreate(writer.Complete(), context, out NetworkObjectUpgradeReader reader), Is.True);
		Exception? exception = Assert.Catch(() => typeof(NetworkObjectUpgradeReader).GetMethod(nameof(NetworkObjectUpgradeReader.Get))!
			.MakeGenericMethod(type).Invoke(reader, ["Value"]));
		Assert.That(exception!.GetBaseException(), Is.TypeOf<InvalidOperationException>().With.Message.Contains("not supported"));
	}

	private static void AssertUnsupportedCollection(Type collectionType, object?[] addArguments) {
		Exception? exception = Assert.Catch(() => {
			INetworkCollection collection = Initialize((INetworkCollection)Activator.CreateInstance(collectionType)!);
			collectionType.GetMethods().Single(method => method.Name == "Add" && method.GetParameters().Length == addArguments.Length).Invoke(collection, addArguments);
			collection.Serialize(new BufferWriter(), new SerializationContext(new TypeCatalogue()), FullOptions);
		});
		Assert.That(exception!.GetBaseException(), Is.TypeOf<InvalidOperationException>().With.Message.Contains("not supported"));
	}

	private static void AssertCollectionRoundTrip<T>(T value) {
		NetworkValueList<T> source = Initialize(new NetworkValueList<T>());
		source.Add(value);
		NetworkValueList<T> target = Initialize(new NetworkValueList<T>());
		Transfer(source, target);
		Assert.That(target.Single(), Is.EqualTo(value));
		NetworkValueDictionary<int, T> dictionary = Initialize(new NetworkValueDictionary<int, T>());
		dictionary.Add(42, value);
		NetworkValueDictionary<int, T> dictionaryTarget = Initialize(new NetworkValueDictionary<int, T>());
		Transfer(dictionary, dictionaryTarget);
		Assert.That(dictionaryTarget[42], Is.EqualTo(value));
	}

	private static void Transfer(INetworkCollection source, INetworkCollection target) {
		BufferWriter writer = new();
		SerializationContext context = new(new TypeCatalogue());
		source.Serialize(writer, context, FullOptions);
		target.Deserialize(writer.GetWrittenSpan(), context);
	}

	private static T Initialize<T>(T collection) where T : INetworkCollection {
		collection.Initialize(new FallbackState(), 0);
		return collection;
	}

	private static void AssertUpgradeRoundTrip<T>(T value) {
		SerializationContext context = new(new TypeCatalogue());
		NetworkObjectUpgradeWriter writer = new(new BufferWriter(), null, context);
		writer.Write("Value", value);
		Assert.That(NetworkObjectUpgradeReader.TryCreate(writer.Complete(), context, out NetworkObjectUpgradeReader reader), Is.True);
		Assert.That(reader.Get<T>("Value"), Is.EqualTo(value));
	}

	private static SerializationOptions FullOptions => new(MemberSelectionMode.All, MemberIdentificationMode.Index);
}
