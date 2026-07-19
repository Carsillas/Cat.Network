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
			Assert.That(generatedSource, Does.Contain("protected static new global::System.Collections.Immutable.ImmutableArray<global::Cat.Network.NetworkPropertyInfo> Properties { get; } = ["));
			Assert.That(generatedSource, Does.Contain("..global::Cat.Network.NetworkEntity.Properties"));
			Assert.That(generatedSource, Does.Contain("Index = global::Cat.Network.NetworkEntity.Properties.Length + 0"));
			Assert.That(generatedSource, Does.Contain("Name = nameof(Health)"));
			Assert.That(generatedSource, Does.Contain("EncodedName = global::System.Collections.Immutable.ImmutableArray.Create(global::System.Text.Encoding.UTF8.GetBytes(nameof(Health)))"));
			Assert.That(generatedSource, Does.Contain("global::System.Collections.Immutable.ImmutableArray<global::Cat.Network.NetworkPropertyInfo> global::Cat.Network.IPartiallySerializable.Properties => Properties;"));
			Assert.That(generatedSource, Does.Contain("public partial global::System.Int32 Health"));
			Assert.That(generatedSource, Does.Contain("get => field;"));
			Assert.That(generatedSource, Does.Contain("set => field = value;"));
			Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
	}

	[Test]
	public void GeneratedPropertiesAreInitializedFromImmediateBaseProperties() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      [NetworkEntity]
		                      public partial class Actor : NetworkEntity {
		                      	[NetworkProperty]
		                      	public partial int Health { get; set; }
		                      }

		                      [NetworkEntity]
		                      public sealed partial class Player : Actor {
		                      	[NetworkProperty]
		                      	public partial int Mana { get; set; }
		                      }
		                      """;

		CSharpCompilation compilation = CreateCompilation(source);
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());

		driver = driver.RunGeneratorsAndUpdateCompilation(
			compilation,
			out Compilation outputCompilation,
			out ImmutableArray<Diagnostic> generatorDiagnostics);

		GeneratorDriverRunResult runResult = driver.GetRunResult();
		string generatedSource = string.Join(Environment.NewLine, runResult.GeneratedTrees.Select(tree => tree.GetText().ToString()));

		Assert.Multiple(() => {
			Assert.That(generatorDiagnostics, Is.Empty);
			Assert.That(runResult.GeneratedTrees, Has.Length.EqualTo(2));
			Assert.That(generatedSource, Does.Contain("..global::Cat.Network.NetworkEntity.Properties"));
			Assert.That(generatedSource, Does.Contain("..global::Game.Actor.Properties"));
			Assert.That(generatedSource, Does.Contain("Index = global::Game.Actor.Properties.Length + 0"));
			Assert.That(generatedSource, Does.Contain("Name = nameof(Mana)"));
			Assert.That(generatedSource, Does.Contain("EncodedName = global::System.Collections.Immutable.ImmutableArray.Create(global::System.Text.Encoding.UTF8.GetBytes(nameof(Mana)))"));
			Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
	}

	[Test]
	public void GeneratedPropertiesPreservePropertyAndAccessorAccessibility() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      [NetworkEntity]
		                      public sealed partial class Player : NetworkEntity {
		                      	[NetworkProperty]
		                      	internal partial int Health { get; private set; }

		                      	[NetworkProperty]
		                      	protected internal partial string Name { get; protected set; }
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
			Assert.That(generatedSource, Does.Contain("internal partial global::System.Int32 Health"));
			Assert.That(generatedSource, Does.Contain("get => field;"));
			Assert.That(generatedSource, Does.Contain("private set => field = value;"));
			Assert.That(generatedSource, Does.Contain("protected internal partial global::System.String Name"));
			Assert.That(generatedSource, Does.Contain("protected set => field = value;"));
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
