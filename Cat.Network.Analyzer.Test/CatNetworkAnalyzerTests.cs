using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cat.Network.Analyzer.Test;

public sealed class CatNetworkAnalyzerTests {
	[Test]
	public async Task ReportsErrorWhenNetworkEntitySubclassIsMissingAttribute() {
		const string source = """
		                      using Cat.Network;

		                      public sealed class Player : NetworkEntity
		                      {
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Has.Length.EqualTo(1));
		Assert.Multiple(() => {
			Assert.That(diagnostics[0].Id, Is.EqualTo("CN0001"));
			Assert.That(diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(diagnostics[0].GetMessage(), Does.Contain("Player"));
		});
	}

	[Test]
	public async Task DoesNotReportErrorWhenNetworkEntitySubclassHasAttribute() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkEntity]
		                      public sealed partial class Player : NetworkEntity
		                      {
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Is.Empty);
	}

	[Test]
	public async Task ReportsErrorWhenNetworkEntitySubclassWithAttributeIsNotPartial() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkEntity]
		                      public sealed class Player : NetworkEntity
		                      {
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Has.Length.EqualTo(1));
		Assert.Multiple(() => {
			Assert.That(diagnostics[0].Id, Is.EqualTo("CN0003"));
			Assert.That(diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(diagnostics[0].GetMessage(), Does.Contain("Player"));
		});
	}

	[Test]
	public async Task DoesNotReportErrorWhenClassHasNoNetworkEntityInheritanceOrAttribute() {
		const string source = """
		                      public sealed class Player
		                      {
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Is.Empty);
	}

	[Test]
	public async Task ReportsErrorWhenNetworkEntityAttributeIsUsedOnNonNetworkEntitySubclass() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkEntity]
		                      public sealed class Player
		                      {
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Has.Length.EqualTo(1));
		Assert.Multiple(() => {
			Assert.That(diagnostics[0].Id, Is.EqualTo("CN0002"));
			Assert.That(diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(diagnostics[0].GetMessage(), Does.Contain("Player"));
		});
	}

	[Test]
	public async Task ReportsErrorWhenNetworkPropertyAttributeIsUsedInNonNetworkEntitySubclass() {
		const string source = """
		                      using Cat.Network;

		                      public sealed partial class Player {
		                      	[NetworkProperty]
		                      	public partial int Health { get; set; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Has.Length.EqualTo(1));
		Assert.Multiple(() => {
			Assert.That(diagnostics[0].Id, Is.EqualTo("CN0004"));
			Assert.That(diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(diagnostics[0].GetMessage(), Does.Contain("Health"));
		});
	}

	[Test]
	public async Task DoesNotReportErrorWhenNetworkPropertyAttributeIsUsedInNetworkEntitySubclass() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkEntity]
		                      public sealed partial class Player : NetworkEntity {
		                      	[NetworkProperty]
		                      	public partial int Health { get; set; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Is.Empty);
	}

	[Test]
	public async Task ReportsErrorWhenNetworkPropertyAttributeIsUsedOnNonPartialProperty() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkEntity]
		                      public sealed partial class Player : NetworkEntity {
		                      	[NetworkProperty]
		                      	public int Health { get; set; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Has.Length.EqualTo(1));
		Assert.Multiple(() => {
			Assert.That(diagnostics[0].Id, Is.EqualTo("CN0005"));
			Assert.That(diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(diagnostics[0].GetMessage(), Does.Contain("Health"));
		});
	}

	private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(string source) {
		CSharpCompilation compilation = CreateCompilation(source);
		ImmutableArray<DiagnosticAnalyzer> analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new CatNetworkAnalyzer());
		CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);

		return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
	}

	private static CSharpCompilation CreateCompilation(string source) {
		SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(source);
		IEnumerable<MetadataReference> references = GetMetadataReferences();

		return CSharpCompilation.Create(
			"AnalyzerTestAssembly",
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
