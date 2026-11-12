using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cat.Network.Generator.Test;

public sealed class RecursiveStructGeneratorTests {
	private static readonly string[] MemberKinds = ["List", "DictionaryValue", "DictionaryKey", "Rpc", "Broadcast", "Property"];

	private static IEnumerable<TestCaseData> RecursiveCases() {
		(string Name, string Declarations, string Type)[] cases = [
			("DirectCycle", "public struct V { public V X; }", "V"),
			("MutualCycle", "public struct V { public W X; } public struct W { public V X; }", "V"),
			("NullableCycle", "public struct V { public V? X; }", "V?"),
			("GenericCycle", "public struct V<T> { public V<T> X; }", "V<int>"),
			("ExpandingGenericCycle", "public struct V<T> { public V<V<T>> X; }", "V<int>"),
			("ExpandingBranchedGenericCycle", "public struct V<T> { public V<V<T>> X; public V<V<T>> Y; }", "V<int>")
		];
		foreach (var test in cases) {
			foreach (string memberKind in MemberKinds) {
				yield return new TestCaseData(memberKind, test.Declarations, test.Type)
					.SetName($"Generator_{test.Name}_{memberKind}");
			}
		}
	}

	[TestCaseSource(nameof(RecursiveCases))]
	public void LeavesRecursiveStructErrorsToTheCompiler(string memberKind, string declarations, string type) {
		CSharpCompilation compilation = CreateCompilation(CreateSource(memberKind, declarations, type));
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out ImmutableArray<Diagnostic> diagnostics);

		Assert.Multiple(() => {
			Assert.That(diagnostics, Is.Empty);
			Assert.That(driver.GetRunResult().Results.All(static result => result.Exception is null), Is.True);
			Assert.That(output.GetDiagnostics().Any(static diagnostic => diagnostic.Id == "CS0523"), Is.True);
		});
	}

	[TestCaseSource(nameof(MemberKinds))]
	public void LeavesUnresolvedStructFieldsToTheCompiler(string memberKind) {
		CSharpCompilation compilation = CreateCompilation(CreateSource(memberKind, "public struct V { public Missing<V> X; }", "V"));
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out ImmutableArray<Diagnostic> diagnostics);

		Assert.Multiple(() => {
			Assert.That(diagnostics, Is.Empty);
			Assert.That(driver.GetRunResult().Results.All(static result => result.Exception is null), Is.True);
			Assert.That(output.GetDiagnostics().Any(static diagnostic => diagnostic.Id == "CS0246"), Is.True);
		});
	}

	[Test]
	public void CanceledGenerationObservesCancellation() {
		CSharpCompilation compilation = CreateCompilation(CreateSource("Rpc", "public struct V { public int Value; }", "V"));
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		Assert.Catch<OperationCanceledException>(() => driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _, cancellation.Token));
	}

	[TestCaseSource(nameof(MemberKinds))]
	public void GeneratesSharedAcyclicStructFields(string memberKind) {
		const string declarations = """
		                            public struct Leaf { public int Value; }
		                            public struct Branch { public Leaf Left; public Leaf Right; }
		                            public struct V { public Branch First; public Branch Second; }
		                            """;
		AssertValidGeneration(CreateSource(memberKind, declarations, "V"));
	}

	[TestCaseSource(nameof(MemberKinds))]
	public void GeneratesFiniteNestedConstructedGenericStructs(string memberKind) {
		const string declarations = """
		                            public struct Box<T> { public T Value; }
		                            public struct V { public Box<Box<int>> First; public Box<Box<int>> Second; }
		                            """;
		AssertValidGeneration(CreateSource(memberKind, declarations, "V"));
	}

	private static void AssertValidGeneration(string source) {
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());
		driver = driver.RunGeneratorsAndUpdateCompilation(CreateCompilation(source), out Compilation output, out ImmutableArray<Diagnostic> diagnostics);

		Assert.Multiple(() => {
			Assert.That(diagnostics, Is.Empty);
			Assert.That(driver.GetRunResult().Results.All(static result => result.Exception is null), Is.True);
			Assert.That(output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
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
}
