using System.Collections.Immutable;
using Cat.Network.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cat.Network.Generator.Test;

public sealed class NetworkObjectMessageGeneratorTests {
	[TestCase("RPC", "NetworkObject?")]
	[TestCase("RPC", "NetworkObject")]
	[TestCase("RPC", "Payload?")]
	[TestCase("Broadcast", "NetworkObject?")]
	[TestCase("Broadcast", "NetworkObject")]
	[TestCase("Broadcast", "Payload?")]
	public async Task NetworkObjectParametersCompileWithAnalyzerAndGenerator(string attribute, string parameterType) {
		string source = $$"""
		                  #nullable enable
		                  using Cat.Network;

		                  [NetworkObject]
		                  public sealed partial class Payload : NetworkObject {
		                      [NetworkProperty]
		                      public partial int Value { get; set; }
		                  }

		                  [NetworkObject]
		                  public sealed partial class Entity : NetworkEntity {
		                      [{{attribute}}]
		                      public partial void Send({{parameterType}} value);
		                  }
		                  """;
		CSharpCompilation compilation = CreateCompilation(source);
		Assert.That(await GetAnalyzerDiagnosticsAsync(compilation), Is.Empty);

		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());
		driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out ImmutableArray<Diagnostic> generatorDiagnostics);

		Assert.Multiple(() => {
			Assert.That(generatorDiagnostics, Is.Empty);
			Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
		Assert.That(await GetAnalyzerDiagnosticsAsync(outputCompilation), Is.Empty);
	}

	[Test]
	public async Task NullableBaseNetworkObjectPropertyRemainsRejectedByAnalyzer() {
		const string source = """
		                      #nullable enable
		                      using Cat.Network;

		                      [NetworkObject]
		                      public sealed partial class Payload : NetworkObject {
		                          [NetworkProperty]
		                          public partial NetworkObject? Child { get; set; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(CreateCompilation(source));
		Assert.That(diagnostics.Select(diagnostic => diagnostic.Id), Is.EqualTo(new[] { "CN0027" }));
	}

	private static Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(Compilation compilation) {
		return compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new CatNetworkAnalyzer()))
			.GetAnalyzerDiagnosticsAsync();
	}

	private static CSharpCompilation CreateCompilation(string source) {
		string trustedPlatformAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
		IEnumerable<MetadataReference> references = trustedPlatformAssemblies.Split(Path.PathSeparator)
			.Select(assemblyPath => MetadataReference.CreateFromFile(assemblyPath))
			.Append(MetadataReference.CreateFromFile(typeof(NetworkObject).Assembly.Location));

		return CSharpCompilation.Create(
			"NetworkObjectMessageTestAssembly",
			new[] { CSharpSyntaxTree.ParseText(source) },
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
	}
}
