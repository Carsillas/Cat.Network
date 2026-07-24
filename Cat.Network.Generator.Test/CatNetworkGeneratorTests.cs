using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cat.Network.Generator.Test;

public sealed class CatNetworkGeneratorTests {
	[Test]
	public void NetworkObjectAttributeIsClassOnlyAndNotInherited() {
		AttributeUsageAttribute usage = typeof(NetworkObjectAttribute)
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
	public void GeneratedRootPropertiesDoNotReferenceObjectProperties() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      [NetworkObjectAttribute]
		                      public partial class Root {
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
		string generatedSource = string.Join(Environment.NewLine, runResult.GeneratedTrees.Select(tree => tree.GetText().ToString()));

		Assert.Multiple(() => {
			Assert.That(generatorDiagnostics, Is.Empty);
			Assert.That(runResult.GeneratedTrees, Has.Length.EqualTo(2));
			Assert.That(generatedSource, Does.Contain("protected static global::System.Collections.Immutable.ImmutableArray<global::Cat.Network.NetworkPropertyInfo> Properties { get; } = ["));
			Assert.That(generatedSource, Does.Not.Contain("..global::System.Object.Properties"));
			Assert.That(generatedSource, Does.Contain("Index = 0 + 0"));
			Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
	}

	[Test]
	public void GeneratorUsesNetworkObjectAttributeMetadataNamePipelineToEmitPartialClassStub() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      [NetworkObjectAttribute]
		                      public sealed partial class Player : NetworkObject {
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
		string generatedSource = string.Join(Environment.NewLine, runResult.GeneratedTrees.Select(tree => tree.GetText().ToString()));

		Assert.Multiple(() => {
			Assert.That(generatorDiagnostics, Is.Empty);
			Assert.That(runResult.GeneratedTrees, Has.Length.EqualTo(2));
			Assert.That(generatedSource, Does.Contain("namespace Game;"));
			Assert.That(generatedSource, Does.Contain("public partial class Player"));
			Assert.That(generatedSource, Does.Contain("// Player"));
			Assert.That(generatedSource, Does.Contain("protected static new global::System.Collections.Immutable.ImmutableArray<global::Cat.Network.NetworkPropertyInfo> Properties { get; } = ["));
			Assert.That(generatedSource, Does.Contain("..global::Cat.Network.NetworkObject.Properties"));
			Assert.That(generatedSource, Does.Contain("Index = global::Cat.Network.NetworkObject.Properties.Length + 0"));
			Assert.That(generatedSource, Does.Contain("Name = nameof(Health)"));
			Assert.That(generatedSource, Does.Contain("EncodedName = global::System.Collections.Immutable.ImmutableArray.Create(global::System.Text.Encoding.UTF8.GetBytes(nameof(Health)))"));
			Assert.That(generatedSource, Does.Contain("public partial global::System.Int32 Health"));
			Assert.That(generatedSource, Does.Contain("get => field;"));
			Assert.That(generatedSource, Does.Contain("set => field = value;"));
			Assert.That(generatedSource, Does.Contain("internal sealed class __CatNetwork_Player_Serializer : global::Cat.Network.INetworkObjectSerializer"));
			Assert.That(generatedSource, Does.Contain("public void Deserialize(global::Cat.Network.NetworkObject target, global::System.ReadOnlySpan<byte> data, global::Cat.Network.SerializationContext context)"));
			Assert.That(generatedSource, Does.Contain("global::Cat.Network.MemberIdentificationMode memberIdentificationMode"));
			Assert.That(generatedSource, Does.Contain("global::System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(data)"));
			Assert.That(generatedSource, Does.Contain("global::System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(data)"));
			Assert.That(generatedSource, Does.Contain("private static void DeserializeHealth(global::Game.Player typedTarget, global::System.ReadOnlySpan<byte> valueData, global::Cat.Network.SerializationContext context)"));
			Assert.That(generatedSource, Does.Contain("case \"Health\":"));
			Assert.That(generatedSource, Does.Contain("Name = \"get_Health\""));
			Assert.That(generatedSource, Does.Contain("Name = \"set_Health\""));
			Assert.That(generatedSource, Does.Contain("private static extern global::System.Int32 GetHealth(global::Game.Player target);"));
			Assert.That(generatedSource, Does.Contain("private static extern void SetHealth(global::Game.Player target, global::System.Int32 value);"));
			Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
	}

	[Test]
	public void GeneratedPropertiesAreInitializedFromImmediateBaseProperties() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      [NetworkObjectAttribute]
		                      public partial class Actor : NetworkObject {
		                      	[NetworkProperty]
		                      	public partial int Health { get; set; }
		                      }

		                      [NetworkObjectAttribute]
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
			Assert.That(runResult.GeneratedTrees, Has.Length.EqualTo(4));
			Assert.That(generatedSource, Does.Contain("..global::Cat.Network.NetworkObject.Properties"));
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

		                      [NetworkObjectAttribute]
		                      public sealed partial class Player : NetworkObject {
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
		string generatedSource = string.Join(Environment.NewLine, runResult.GeneratedTrees.Select(tree => tree.GetText().ToString()));

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

	[Test]
	public void GeneratedSerializerIncludesNetworkObjectUpdateHandling() {
		const string source = """
		                      using Cat.Network;

		                      namespace Game;

		                      [NetworkObjectAttribute]
		                      public partial class Child : NetworkObject {
		                      	[NetworkProperty]
		                      	public partial int Value { get; set; }
		                      }

		                      [NetworkObjectAttribute]
		                      public partial class Parent : NetworkObject {
		                      	[NetworkProperty]
		                      	public partial Child? Child { get; set; }
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
			Assert.That(runResult.GeneratedTrees, Has.Length.EqualTo(4));
			Assert.That(generatedSource, Does.Contain("global::Cat.Network.NetworkObjectUpdateMode NetworkObjectUpdateMode = (global::Cat.Network.NetworkObjectUpdateMode)valueData[0];"));
			Assert.That(generatedSource, Does.Contain("case global::Cat.Network.NetworkObjectUpdateMode.Modify:"));
			Assert.That(generatedSource, Does.Contain("case global::Cat.Network.NetworkObjectUpdateMode.Replace:"));
			Assert.That(generatedSource, Does.Contain("case global::Cat.Network.NetworkObjectUpdateMode.Clear:"));
			Assert.That(generatedSource, Does.Contain("global::Game.Child? currentTarget = GetChild(typedTarget);"));
			Assert.That(generatedSource, Does.Contain("context.TypeCatalogue.TryFindSerializer(currentTarget.GetType(), out global::Cat.Network.INetworkObjectSerializer? nestedSerializer)"));
			Assert.That(generatedSource, Does.Contain("context.TypeCatalogue.TryFindType(replacementTypeId, out global::System.Type? replacementType)"));
			Assert.That(generatedSource, Does.Contain("global::System.Activator.CreateInstance(replacementType) is not global::Game.Child? replacementTarget"));
			Assert.That(generatedSource, Does.Contain("Name = \"get_Child\""));
			Assert.That(generatedSource, Does.Contain("Name = \"set_Child\""));
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

		yield return MetadataReference.CreateFromFile(typeof(NetworkObject).Assembly.Location);
	}
}
