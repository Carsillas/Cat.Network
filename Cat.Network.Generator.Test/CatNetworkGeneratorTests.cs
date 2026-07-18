using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cat.Network.Generator.Test;

public sealed class CatNetworkGeneratorTests {
	[Test]
	public void NetworkEntityAttributeIsClassOnlyAndNotInherited() {
		AttributeUsageAttribute usage = typeof(NetworkEntityAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.OfType<AttributeUsageAttribute>()
			.Single();

		Assert.Multiple(() => {
			Assert.That(usage.ValidOn, Is.EqualTo(AttributeTargets.Class));
			Assert.That(usage.Inherited, Is.False);
		});
	}

	[Test]
	public void NetworkPropertyAttributeIsPropertyOnlyAndNotInherited() {
		AttributeUsageAttribute usage = typeof(NetworkPropertyAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.OfType<AttributeUsageAttribute>()
			.Single();

		Assert.Multiple(() => {
			Assert.That(usage.ValidOn, Is.EqualTo(AttributeTargets.Property));
			Assert.That(usage.Inherited, Is.False);
		});
	}

	[Test]
	public void GeneratorUsesAttributeMetadataNamePipelineToEmitPartialClassStub() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      [NetworkEntity]
		                      public sealed partial class Player : NetworkEntity {
		                      	[NetworkProperty]
		                      	public partial int Health { get; set; }
		                      }
		                      """;

		CSharpCompilation compilation = CreateCompilation(source);
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());

		driver = driver.RunGeneratorsAndUpdateCompilation(
			compilation,
			out Compilation outputCompilation,
			out ImmutableArray<Diagnostic> generatorDiagnostics);

		GeneratorDriverRunResult runResult = driver.GetRunResult();
		string generatedSource = runResult.GeneratedTrees.Single().GetText().ToString();

		Assert.Multiple(() => {
			Assert.That(generatorDiagnostics, Is.Empty);
			Assert.That(runResult.GeneratedTrees, Has.Length.EqualTo(1));
			Assert.That(generatedSource, Does.Contain("namespace Game;"));
			Assert.That(generatedSource, Does.Contain("public partial class Player"));
			Assert.That(generatedSource, Does.Contain("// Player"));
			Assert.That(generatedSource, Does.Contain("public partial global::System.Int32 Health"));
			Assert.That(generatedSource, Does.Contain("get => field;"));
			Assert.That(generatedSource, Does.Contain("set => field = value;"));
			Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
	}

	private static CSharpCompilation CreateCompilation(string source) {
		SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
		IEnumerable<MetadataReference> references = GetMetadataReferences();

		return CSharpCompilation.Create(
			"GeneratorTestAssembly",
			new[] { syntaxTree },
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
	}

	private static IEnumerable<MetadataReference> GetMetadataReferences() {
		string trustedPlatformAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;

		foreach (string assemblyPath in trustedPlatformAssemblies.Split(Path.PathSeparator)) yield return MetadataReference.CreateFromFile(assemblyPath);

		yield return MetadataReference.CreateFromFile(typeof(NetworkEntity).Assembly.Location);
	}
}
