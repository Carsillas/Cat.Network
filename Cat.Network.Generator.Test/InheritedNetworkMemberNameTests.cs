using System.Collections.Immutable;
using Cat.Network.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cat.Network.Generator.Test;

public sealed class InheritedNetworkMemberNameTests {
	private static readonly ImmutableArray<MetadataReference> References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
		.Split(Path.PathSeparator)
		.Append(typeof(NetworkObject).Assembly.Location)
		.Distinct(StringComparer.OrdinalIgnoreCase)
		.Select(static path => MetadataReference.CreateFromFile(path))
		.ToImmutableArray<MetadataReference>();

	[Test, Combinatorial]
	public async Task ReplicatedNamesMustBeUniqueAcrossMemberKindsAndAncestors(
		[Values("Property", "List", "Dictionary")] string baseKind,
		[Values("Property", "List", "Dictionary")] string derivedKind,
		[Values(false, true)] bool multilevel,
		[Values(false, true)] bool privateBase) {
		string source = $$"""
			using Cat.Network;
			[NetworkObject] public partial class Base : NetworkObject {
				{{Member(baseKind, "Value", privateBase ? "private" : "public")}}
			}
			{{(multilevel ? "[NetworkObject] public partial class Middle : Base { }" : "")}}
			[NetworkObject] public partial class Derived : {{(multilevel ? "Middle" : "Base")}} {
				{{Member(derivedKind, "Value", "public")}}
			}
			[NetworkObject] public partial class Leaf : Derived { }
			[NetworkObject] public partial class Unrelated : NetworkObject {
				[NetworkProperty] public partial int Other { get; set; }
			}
			""";

		CompilationResult result = await CompileAsync(source);
		TestContext.Out.WriteLine($"Input analyzer IDs: {string.Join(", ", result.InputDiagnostics.Select(static diagnostic => diagnostic.Id))}; generated compilation errors: {result.CompilerErrors.Length}");
		Assert.Multiple(() => {
			AssertCollision(result.InputDiagnostics, source, "Base");
			AssertCollision(result.OutputDiagnostics, source, "Base");
			Assert.That(result.GeneratorDiagnostics, Is.Empty, "Invalid schemas must not crash the generator (CS8785).");
			Assert.That(result.GeneratedPaths, Does.Not.Contain("Derived.g.cs"));
			Assert.That(result.GeneratedPaths, Does.Not.Contain("Derived_Serializer.g.cs"));
			Assert.That(result.GeneratedPaths, Does.Not.Contain("Leaf.g.cs"));
			Assert.That(result.GeneratedPaths, Does.Not.Contain("Leaf_Serializer.g.cs"));
			Assert.That(result.GeneratedPaths, Does.Contain("Base.g.cs"));
			Assert.That(result.GeneratedPaths, Does.Contain("Base_Serializer.g.cs"));
			Assert.That(result.GeneratedPaths, Does.Contain("Unrelated_Serializer.g.cs"));
			Assert.That(result.CompilerErrors.Where(static diagnostic => diagnostic.Location.SourceTree?.FilePath.EndsWith(".g.cs", StringComparison.Ordinal) == true), Is.Empty);
		});
	}

	[Test, Combinatorial]
	public async Task DistinctReplicatedNamesRemainSupported(
		[Values("Property", "List", "Dictionary")] string baseKind,
		[Values("Property", "List", "Dictionary")] string derivedKind,
		[Values(false, true)] bool privateBase) {
		string source = $$"""
			using Cat.Network;
			[NetworkObject] public partial class Base : NetworkObject {
				{{Member(baseKind, "Value", privateBase ? "private" : "public")}}
			}
			[NetworkObject] public partial class Middle : Base { }
			[NetworkObject] public partial class Derived : Middle {
				{{Member(derivedKind, "Other", "public")}}
			}
			""";

		AssertSupported(await CompileAsync(source));
	}

	[TestCase("Property")]
	[TestCase("List")]
	[TestCase("Dictionary")]
	public async Task ReplicatedMemberMayHideAnOrdinaryProperty(string kind) {
		string source = $$"""
			using Cat.Network;
			[NetworkObject] public partial class Base : NetworkObject {
				public int Value { get; set; }
			}
			[NetworkObject] public partial class Derived : Base {
				{{Member(kind, "Value", "public")}}
			}
			""";

		AssertSupported(await CompileAsync(source));
	}

	[Test]
	public async Task OrdinaryPropertyHidingAReplicatedPropertyDoesNotAddASchemaName() {
		const string source = """
			using Cat.Network;
			[NetworkObject] public partial class Base : NetworkObject {
				[NetworkProperty] public partial int Value { get; set; }
			}
			[NetworkObject] public partial class Derived : Base {
				public new int Value { get; set; }
			}
			""";

		AssertSupported(await CompileAsync(source));
	}

	[Test]
	public async Task OrdinaryIntermediatePropertyDoesNotMaskAnInheritedSchemaName() {
		const string source = """
			using Cat.Network;
			[NetworkObject] public partial class Base : NetworkObject {
				[NetworkCollection] private partial NetworkList<int> Value { get; }
			}
			[NetworkObject] public partial class Middle : Base {
				public int Value { get; set; }
			}
			[NetworkObject] public partial class Derived : Middle {
				[NetworkProperty] public partial int Value { get; set; }
			}
			""";

		CompilationResult result = await CompileAsync(source);
		Assert.Multiple(() => {
			AssertCollision(result.InputDiagnostics, source, "Base");
			AssertCollision(result.OutputDiagnostics, source, "Base");
			Assert.That(result.GeneratorDiagnostics, Is.Empty);
			Assert.That(result.GeneratedPaths, Does.Not.Contain("Derived_Serializer.g.cs"));
		});
	}

	private static void AssertCollision(ImmutableArray<Diagnostic> diagnostics, string source, string declaringType) {
		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { "CN0007" }), "Report one member-name error, without analyzer failures (AD0001).");
		Diagnostic? collision = diagnostics.FirstOrDefault(static diagnostic => diagnostic.Id == "CN0007");
		if (collision is not null) {
			Assert.That(collision.Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(collision.Location.SourceSpan.Start, Is.EqualTo(source.LastIndexOf("Value {", StringComparison.Ordinal)));
			Assert.That(collision.GetMessage(), Does.Contain("Value").And.Contain(declaringType));
		}
	}

	private static void AssertSupported(CompilationResult result) {
		Assert.Multiple(() => {
			Assert.That(result.InputDiagnostics, Is.Empty);
			Assert.That(result.OutputDiagnostics, Is.Empty);
			Assert.That(result.GeneratorDiagnostics, Is.Empty);
			Assert.That(result.CompilerErrors, Is.Empty);
			Assert.That(result.GeneratedPaths, Does.Contain("Derived_Serializer.g.cs"));
		});
	}

	private static string Member(string kind, string name, string modifiers) => kind switch {
		"Property" => $"[NetworkProperty] {modifiers} partial int {name} {{ get; set; }}",
		"List" => $"[NetworkCollection] {modifiers} partial NetworkList<int> {name} {{ get; }}",
		"Dictionary" => $"[NetworkCollection] {modifiers} partial NetworkDictionary<int, int> {name} {{ get; }}",
		_ => throw new ArgumentOutOfRangeException(nameof(kind))
	};

	private static async Task<CompilationResult> CompileAsync(string source) {
		CSharpCompilation compilation = CSharpCompilation.Create(
			"InheritedNetworkMemberNameTestAssembly",
			new[] { CSharpSyntaxTree.ParseText(source) },
			References,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
		ImmutableArray<DiagnosticAnalyzer> analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new CatNetworkAnalyzer());
		ImmutableArray<Diagnostic> inputDiagnostics = await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out ImmutableArray<Diagnostic> generatorDiagnostics);
		ImmutableArray<Diagnostic> outputDiagnostics = await output.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
		return new CompilationResult(
			inputDiagnostics,
			outputDiagnostics,
			generatorDiagnostics,
			output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToImmutableArray(),
			driver.GetRunResult().GeneratedTrees.Select(static tree => Path.GetFileName(tree.FilePath)).ToImmutableArray());
	}

	private sealed record CompilationResult(
		ImmutableArray<Diagnostic> InputDiagnostics,
		ImmutableArray<Diagnostic> OutputDiagnostics,
		ImmutableArray<Diagnostic> GeneratorDiagnostics,
		ImmutableArray<Diagnostic> CompilerErrors,
		ImmutableArray<string> GeneratedPaths);
}
