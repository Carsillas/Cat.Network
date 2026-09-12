using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Cat.Network.Analyzer.Test;

public sealed class NullableCollectionFieldAnalyzerTests {
	[Test]
	public async Task RejectsUnsupportedFieldsBehindNullableStructs(
		[Values("NetworkList<Value>", "NetworkDictionary<int, Value>")] string collectionType,
		[Values("System.Uri", "int[]", "Child", "Child?")] string fieldType,
		[Values(false, true)] bool multipleLevels) {
		string structs = $$"""
		                   public struct Value { public {{(multipleLevels ? "Middle?" : "Inner?")}} Field; }
		                   public struct Middle { public Inner? Field; }
		                   public struct Inner { public {{fieldType}} Field; }
		                   [NetworkObject] public partial class Child : NetworkObject { }
		                   """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(CreateSource(collectionType, structs));

		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { "CN0015" }));
	}

	[Test]
	public async Task AcceptsSupportedNullableFieldsAndRepeatedConstructedStructs(
		[Values("NetworkList<Value>", "NetworkDictionary<int, Value>")] string collectionType) {
		const string structs = """
		                       public struct Box<T> { public T Field; }
		                       public struct Inner { public int? Number; public System.Guid? Id; public string? Label; }
		                       public struct Middle { public Inner? First; public Inner? Second; }
		                       public struct Value {
		                           public Middle? Field;
		                           public Box<Box<int>>? Finite;
		                           public Box<Box<int>>? Sibling;
		                       }
		                       """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(CreateSource(collectionType, structs));

		Assert.That(diagnostics, Is.Empty);
	}

	[Test]
	public async Task RejectsNullableValueFieldsInDictionaryKeys(
		[Values("int?", "System.Guid?", "Inner?")] string fieldType,
		[Values(false, true)] bool multipleLevels) {
		string structs = $$"""
		                   public struct Inner { public int Number; }
		                   public struct Middle { public {{fieldType}} Field; }
		                   public struct Value { public {{(multipleLevels ? "Middle" : fieldType)}} Field; }
		                   """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(CreateSource("NetworkDictionary<Value, int>", structs));

		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { "CN0016" }));
	}

	[Test]
	public async Task AcceptsNullableReferenceFieldsInDictionaryKeys() {
		const string structs = """
		                       public struct Inner { public string? Label; }
		                       public struct Value { public Inner First; public Inner Second; }
		                       """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(CreateSource("NetworkDictionary<Value, int>", structs));

		Assert.That(diagnostics, Is.Empty);
	}

	[Test]
	public async Task RejectsNullableRecursiveStructsWithoutAnalyzerFailure(
		[Values("NetworkList<Value>", "NetworkDictionary<int, Value>")] string collectionType,
		[Values(
			"public struct Value { public Value? Field; }",
			"public struct Value { public Inner? Field; } public struct Inner { public Value? Field; }",
			"public struct Value { public Expanding<int>? Field; } public struct Expanding<T> { public Expanding<Expanding<T>>? Field; }")]
		string structs) {
		using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(10));

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(
			CreateSource(collectionType, structs), cancellation.Token).WaitAsync(TimeSpan.FromSeconds(15));

		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { "CN0015" }));
	}

	private static string CreateSource(string collectionType, string structs) {
		return $$"""
		         #nullable enable
		         using Cat.Network;
		         {{structs}}
		         [NetworkObject]
		         public sealed partial class Owner : NetworkObject {
		             [NetworkCollection]
		             public partial {{collectionType}} Values { get; }
		         }
		         """;
	}
}
