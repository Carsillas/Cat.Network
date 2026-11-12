using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cat.Network.Analyzer.Test;

public sealed class RecursiveStructAnalyzerTests {
	private static readonly string[] MemberKinds = ["List", "DictionaryValue", "DictionaryKey", "Rpc", "Broadcast", "Property"];

	private static IEnumerable<TestCaseData> RecursiveCases() {
		(string Name, string Declarations, string Type)[] cases = [
			("DirectCycle", "public struct V { public V X; }", "V"),
			("MutualCycle", "public struct V { public W X; } public struct W { public V X; }", "V"),
			("NullableItemCycle", "public struct V { public V X; }", "V?"),
			("GenericCycle", "public struct V<T> { public V<T> X; }", "V<int>"),
			("ExpandingGenericCycle", "public struct V<T> { public V<V<T>> X; }", "V<int>"),
			("ExpandingBranchedGenericCycle", "public struct V<T> { public V<V<T>> X; public V<V<T>> Y; }", "V<int>")
		];
		foreach (var test in cases) {
			foreach (string memberKind in MemberKinds) {
				yield return new TestCaseData(memberKind, test.Declarations, test.Type)
					.SetName($"{test.Name}_{memberKind}");
			}
		}
	}

	[TestCaseSource(nameof(RecursiveCases))]
	public async Task ReportsUnsupportedTypeForRecursiveStruct(string memberKind, string declarations, string type) {
		CSharpCompilation compilation = CreateCompilation(CreateSource(memberKind, declarations, type));
		ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(compilation);

		Assert.Multiple(() => {
			Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { UnsupportedDiagnostic(memberKind) }));
			Assert.That(compilation.GetDiagnostics().Any(static diagnostic => diagnostic.Id == "CS0523"), Is.True,
				"The regression must analyze the editing-time symbols of an invalid recursive struct.");
		});
	}

	[TestCaseSource(nameof(MemberKinds))]
	public async Task NullableFieldCycleLeavesCompilerDiagnosticWithoutAnalyzerFailure(string memberKind) {
		CSharpCompilation compilation = CreateCompilation(CreateSource(memberKind, "public struct V { public V? X; }", "V"));
		ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(compilation);

		Assert.Multiple(() => {
			// Collection nullable-field validation is a separate policy; either the existing
			// unsupported-type diagnostic or the compiler's cycle diagnostic is sufficient.
			Assert.That(diagnostics.All(diagnostic => diagnostic.Id == UnsupportedDiagnostic(memberKind)), Is.True,
				string.Join(Environment.NewLine, diagnostics));
			Assert.That(compilation.GetDiagnostics().Any(static diagnostic => diagnostic.Id == "CS0523"), Is.True);
		});
	}

	[TestCaseSource(nameof(MemberKinds))]
	public async Task ReportsUnsupportedTypeForUnresolvedStructField(string memberKind) {
		CSharpCompilation compilation = CreateCompilation(CreateSource(memberKind, "public struct V { public Missing<V> X; }", "V"));
		ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(compilation);

		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { UnsupportedDiagnostic(memberKind) }));
	}

	[TestCaseSource(nameof(MemberKinds))]
	public async Task AcceptsSharedAcyclicStructFields(string memberKind) {
		const string declarations = """
		                            public struct Leaf { public int Value; }
		                            public struct Branch { public Leaf Left; public Leaf Right; }
		                            public struct V { public Branch First; public Branch Second; }
		                            """;
		ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(CreateCompilation(CreateSource(memberKind, declarations, "V")));

		Assert.That(diagnostics, Is.Empty);
	}

	[TestCaseSource(nameof(MemberKinds))]
	public async Task AcceptsFiniteNestedConstructedGenericStructs(string memberKind) {
		const string declarations = """
		                            public struct Box<T> { public T Value; }
		                            public struct V { public Box<Box<int>> First; public Box<Box<int>> Second; }
		                            """;
		ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(CreateCompilation(CreateSource(memberKind, declarations, "V")));

		Assert.That(diagnostics, Is.Empty);
	}

	[Test]
	public void CanceledAnalysisObservesCancellation() {
		CSharpCompilation compilation = CreateCompilation(CreateSource("List", "public struct V { public int Value; }", "V"));
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.CatchAsync<OperationCanceledException>(async () => await GetAnalyzerDiagnosticsAsync(compilation, cancellation.Token));
	}

	[TestCaseSource(nameof(MemberKinds))]
	public async Task AcceptsAcyclicStructsAtTheNestingLimit(string memberKind) {
		string declarations = CreateNestedStructs(128);
		ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(CreateCompilation(CreateSource(memberKind, declarations, "V0")));

		Assert.That(diagnostics, Is.Empty);
	}

	[TestCaseSource(nameof(MemberKinds))]
	public async Task ReportsUnsupportedTypeBeyondTheNestingLimit(string memberKind) {
		string declarations = CreateNestedStructs(129);
		ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(CreateCompilation(CreateSource(memberKind, declarations, "V0")));

		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { UnsupportedDiagnostic(memberKind) }));
	}

	private static string CreateNestedStructs(int count) {
		return string.Join(Environment.NewLine, Enumerable.Range(0, count).Select(index =>
			$"public struct V{index} {{ public {(index + 1 == count ? "int" : $"V{index + 1}")} Value; }}"));
	}

	private static string CreateSource(string memberKind, string declarations, string type) {
		string member = memberKind switch {
			"List" => $"[NetworkCollection] public partial NetworkList<{type}> Items {{ get; }}",
			"DictionaryValue" => $"[NetworkCollection] public partial NetworkDictionary<int, {type}> Items {{ get; }}",
			"DictionaryKey" => $"[NetworkCollection] public partial NetworkDictionary<{type}, int> Items {{ get; }}",
			"Rpc" => $"[RPC] public partial void Send({type} value);",
			"Broadcast" => $"[Broadcast] public partial void Send({type} value);",
			"Property" => $"[NetworkProperty] public partial {type} Value {{ get; set; }}",
			_ => throw new ArgumentOutOfRangeException(nameof(memberKind))
		};
		return $$"""
		         using Cat.Network;
		         {{declarations}}
		         [NetworkObject]
		         public partial class Player : NetworkEntity {
		             {{member}}
		         }
		         """;
	}

	private static string UnsupportedDiagnostic(string memberKind) {
		return memberKind switch {
			"DictionaryKey" => "CN0016",
			"Rpc" or "Broadcast" => "CN0023",
			"Property" => "CN0028",
			_ => "CN0015"
		};
	}

	private static CSharpCompilation CreateCompilation(string source) {
		string trustedPlatformAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
		IEnumerable<MetadataReference> references = trustedPlatformAssemblies.Split(Path.PathSeparator)
			.Append(typeof(NetworkObject).Assembly.Location)
			.Distinct()
			.Select(static path => MetadataReference.CreateFromFile(path));
		return CSharpCompilation.Create("RecursiveStructTestAssembly",
			[CSharpSyntaxTree.ParseText(source)], references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
	}

	private static Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(CSharpCompilation compilation, CancellationToken cancellationToken = default) {
		return compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new CatNetworkAnalyzer()))
			.GetAnalyzerDiagnosticsAsync(cancellationToken);
	}
}
