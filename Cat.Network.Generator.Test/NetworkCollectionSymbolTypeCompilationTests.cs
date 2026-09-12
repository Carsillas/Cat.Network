using System.Collections.Immutable;
using Cat.Network.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cat.Network.Generator.Test;

public sealed class NetworkCollectionSymbolTypeCompilationTests {
	[TestCase("NetworkList<int[]>", "CN0015")]
	[TestCase("NetworkDictionary<int, int[]>", "CN0015")]
	[TestCase("NetworkDictionary<int[], int>", "CN0016")]
	public async Task RejectsUnsupportedArraySchemaEvenWhenGeneratedCodeCompiles(string collectionType, string diagnosticId) {
		Compilation compilation = GenerateCollection(collectionType);
		ImmutableArray<Diagnostic> diagnostics = await compilation
			.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new CatNetworkAnalyzer()))
			.GetAllDiagnosticsAsync();

		Assert.Multiple(() => {
			Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Does.Not.Contain("AD0001"));
			Assert.That(diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Select(static diagnostic => diagnostic.Id),
				Is.EqualTo(new[] { diagnosticId }));
		});
	}

	[TestCase("NetworkList<int>")]
	[TestCase("NetworkList<string?>")]
	[TestCase("NetworkList<Guid>")]
	[TestCase("NetworkList<int?>")]
	[TestCase("NetworkList<Entry>")]
	[TestCase("NetworkList<Entry?>")]
	[TestCase("NetworkList<NetworkObject>")]
	[TestCase("NetworkList<Child>")]
	[TestCase("NetworkDictionary<string, int>")]
	[TestCase("NetworkDictionary<Entry, Child>")]
	[TestCase("NetworkDictionary<Guid, Entry?>")]
	public async Task SupportedCollectionSchemasStillAnalyzeAndCompile(string collectionType) {
		Compilation compilation = GenerateCollection(collectionType);
		ImmutableArray<Diagnostic> diagnostics = await compilation
			.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new CatNetworkAnalyzer()))
			.GetAnalyzerDiagnosticsAsync();

		Assert.That(diagnostics, Is.Empty);
		using MemoryStream assembly = new();
		Assert.That(compilation.Emit(assembly).Success, Is.True);
	}

	private static Compilation GenerateCollection(string collectionType) {
		string source = $$"""
		                  #nullable enable
		                  using System;
		                  using Cat.Network;

		                  public struct Entry {
		                      public int Score;
		                      public Guid SessionId;
		                  }

		                  [NetworkObject]
		                  public partial class Child : NetworkObject { }

		                  [NetworkObject]
		                  public partial class Player : NetworkObject {
		                      [NetworkCollection]
		                      public partial {{collectionType}} Items { get; }
		                  }
		                  """;
		string trustedPlatformAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
		IEnumerable<MetadataReference> references = trustedPlatformAssemblies.Split(Path.PathSeparator)
			.Append(typeof(NetworkObject).Assembly.Location)
			.Distinct()
			.Select(static path => MetadataReference.CreateFromFile(path));
		CSharpCompilation compilation = CSharpCompilation.Create(
			"CollectionSymbolTypeTestAssembly",
			new[] { CSharpSyntaxTree.ParseText(source) },
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation outputCompilation, out ImmutableArray<Diagnostic> generatorDiagnostics);

		Assert.Multiple(() => {
			Assert.That(generatorDiagnostics, Is.Empty);
			Assert.That(driver.GetRunResult().Results.Single().Exception, Is.Null);
			Assert.That(outputCompilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
		return outputCompilation;
	}
}
