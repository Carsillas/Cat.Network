using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cat.Network.Analyzer.Test;

public sealed class StructFallbackAnalyzerTests {
	private const string StructDeclarations = """
		public struct Empty { }
		public struct Stateless { public static int Shared; public int Computed => 42; }
		public struct Hidden { private int value; public int Value => value; }
		public struct AutoProperty { public int Value { get; set; } }
		public struct PublicFields { public int Value; public System.Guid Id; }
		public struct MixedFields { public int Value; private int local; public int Local => local; }
		public struct Nested<T> { public T Value; }
		""";

	[TestCase("decimal")]
	[TestCase("System.DateTime")]
	[TestCase("char")]
	[TestCase("System.TimeSpan")]
	[TestCase("Hidden")]
	[TestCase("AutoProperty")]
	public async Task RejectsHiddenStateAtRootAndWithinPublicFields(string typeName) {
		foreach (string shape in new[] { typeName, $"Nested<{typeName}>", $"Nested<Nested<{typeName}>>" }) {
			ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(CreateSource(shape));
			AssertRejected(diagnostics, shape);
		}
	}

	[TestCase("decimal?")]
	[TestCase("System.DateTime?")]
	[TestCase("char?")]
	public async Task RejectsNullableHiddenStateAtRootAndWithinPublicFields(string typeName) {
		foreach (string shape in new[] { typeName, $"Nested<{typeName}>" }) {
			ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(CreateSource(shape, includeKey: false));
			AssertRejected(diagnostics, shape, includeKey: false);
		}
	}

	[TestCase("bool")]
	[TestCase("byte")]
	[TestCase("sbyte")]
	[TestCase("short")]
	[TestCase("ushort")]
	[TestCase("int")]
	[TestCase("uint")]
	[TestCase("long")]
	[TestCase("ulong")]
	[TestCase("float")]
	[TestCase("double")]
	[TestCase("string")]
	[TestCase("System.Guid")]
	[TestCase("Empty")]
	[TestCase("Stateless")]
	[TestCase("PublicFields")]
	[TestCase("MixedFields")]
	[TestCase("Nested<Empty>")]
	[TestCase("Nested<PublicFields>")]
	public async Task AcceptsExplicitCodecsPublicFieldsAndEmptyStructs(string typeName) {
		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(CreateSource(typeName));
		Assert.That(diagnostics, Is.Empty);
	}

	[TestCase("int?")]
	[TestCase("System.Guid?")]
	[TestCase("Empty?")]
	[TestCase("PublicFields?")]
	public async Task AcceptsNullableSupportedTypesAtRootAndWithinPublicFields(string typeName) {
		foreach (string shape in new[] { typeName, $"Nested<{typeName}>" }) {
			ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(CreateSource(shape, includeKey: false));
			Assert.That(diagnostics, Is.Empty);
		}
	}

	[TestCase("Hidden", true)]
	[TestCase("AutoProperty", true)]
	[TestCase("Empty", false)]
	[TestCase("Stateless", false)]
	[TestCase("PublicFields", false)]
	[TestCase("MixedFields", false)]
	public async Task AppliesTheSameRuleToReferencedStructs(string typeName, bool rejected) {
		MetadataReference[] references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
			.Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path))
			.Append(MetadataReference.CreateFromFile(typeof(NetworkObject).Assembly.Location)).ToArray();
		CSharpCompilation library = CSharpCompilation.Create("StructLibrary", [CSharpSyntaxTree.ParseText(StructDeclarations)], references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
		using MemoryStream stream = new();
		Assert.That(library.Emit(stream).Success, Is.True);
		CSharpCompilation consumer = CSharpCompilation.Create("StructConsumer", [CSharpSyntaxTree.ParseText(CreateSource(typeName, includeDeclarations: false))],
			references.Append(MetadataReference.CreateFromImage(stream.ToArray())), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
		ImmutableArray<Diagnostic> diagnostics = await consumer.WithAnalyzers([new CatNetworkAnalyzer()]).GetAnalyzerDiagnosticsAsync();
		if (rejected) {
			AssertRejected(diagnostics, typeName);
		} else {
			Assert.That(diagnostics, Is.Empty);
		}
	}

	private static string CreateSource(string typeName, bool includeKey = true, bool includeDeclarations = true) => $$"""
		using Cat.Network;
		{{(includeDeclarations ? StructDeclarations : "")}}
		[NetworkObject]
		public partial class State : NetworkEntity {
			[NetworkProperty] public partial {{typeName}} Value { get; set; }
			[NetworkCollection] public partial NetworkList<{{typeName}}> Items { get; }
			[NetworkCollection] public partial NetworkDictionary<int, {{typeName}}> Values { get; }
			{{(includeKey ? $"[NetworkCollection] public partial NetworkDictionary<{typeName}, int> Keys {{ get; }}" : "")}}
			[RPC] public partial void Send({{typeName}} value);
			[Broadcast] public partial void Share({{typeName}} value);
		}
		""";

	private static void AssertRejected(ImmutableArray<Diagnostic> diagnostics, string shape, bool includeKey = true) {
		string[] expected = includeKey ? ["CN0015", "CN0015", "CN0016", "CN0023", "CN0023", "CN0028"] : ["CN0015", "CN0015", "CN0023", "CN0023", "CN0028"];
		Assert.Multiple(() => {
			Assert.That(diagnostics.Select(diagnostic => diagnostic.Id), Is.EquivalentTo(expected), shape);
			Assert.That(diagnostics.All(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Location.IsInSource), Is.True);
			Assert.That(diagnostics.All(diagnostic => diagnostic.GetMessage().Contains("supported", StringComparison.Ordinal)), Is.True);
		});
	}
}
