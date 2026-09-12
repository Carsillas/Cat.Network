using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Cat.Network.Analyzer.Test;

public sealed class UpgradeToAttributeAnalyzerTests {
	[Test]
	public async Task DoesNotReportErrorWhenUpgradeMethodHasExpectedShape() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObjectAttribute(Version = 2)]
		                      public sealed partial class Player : NetworkObject {
		                      	[UpgradeTo(2)]
		                      	private static void UpgradeToVersion2(NetworkObjectUpgradeReader reader, NetworkObjectUpgradeWriter writer) {
		                      	}
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Is.Empty);
	}

	[TestCase("await Task.Yield();", TestName = "ReportsErrorWhenUpgradeMethodIsAsyncVoidWithAwait")]
	[TestCase("", TestName = "ReportsErrorWhenUpgradeMethodIsAsyncVoidWithoutAwait")]
	public async Task ReportsErrorWhenUpgradeMethodIsAsyncVoid(string awaitStatement) {
		string source = $$"""
		                  using Cat.Network;
		                  using System.Threading.Tasks;

		                  [NetworkObjectAttribute(Version = 1)]
		                  public sealed partial class Player : NetworkObject {
		                      [UpgradeTo(1)]
		                      private static async void UpgradeToVersion1(NetworkObjectUpgradeReader reader, NetworkObjectUpgradeWriter writer) {
		                          {{awaitStatement}}
		                          writer.CopyExcept();
		                      }
		                  }
		                  """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		AssertInvalidUpgradeSignature(diagnostics);
	}

	[TestCase(true)]
	[TestCase(false)]
	public async Task ReportsErrorWhenPartialUpgradeImplementationIsAsync(bool attributeOnDefinition) {
		string source = $$"""
		                  using Cat.Network;
		                  using System.Threading.Tasks;

		                  [NetworkObjectAttribute(Version = 1)]
		                  public sealed partial class Player : NetworkObject {
		                      {{(attributeOnDefinition ? "[UpgradeTo(1)]" : "")}}
		                      private static partial void UpgradeToVersion1(NetworkObjectUpgradeReader reader, NetworkObjectUpgradeWriter writer);

		                      {{(attributeOnDefinition ? "" : "[UpgradeTo(1)]")}}
		                      private static async partial void UpgradeToVersion1(NetworkObjectUpgradeReader reader, NetworkObjectUpgradeWriter writer) {
		                          await Task.Yield();
		                          writer.CopyExcept();
		                      }
		                  }
		                  """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		AssertInvalidUpgradeSignature(diagnostics);
	}

	[Test]
	public async Task DoesNotReportErrorWhenPartialUpgradeImplementationIsSynchronous() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObjectAttribute(Version = 1)]
		                      public sealed partial class Player : NetworkObject {
		                          [UpgradeTo(1)]
		                          private static partial void UpgradeToVersion1(NetworkObjectUpgradeReader reader, NetworkObjectUpgradeWriter writer);

		                          private static partial void UpgradeToVersion1(NetworkObjectUpgradeReader reader, NetworkObjectUpgradeWriter writer) {
		                              writer.CopyExcept();
		                          }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Is.Empty);
	}

	[TestCase("Task", "return Task.CompletedTask;")]
	[TestCase("async Task", "await Task.Yield();")]
	[TestCase("Task<int>", "return Task.FromResult(1);")]
	[TestCase("async Task<int>", "await Task.Yield(); return 1;")]
	public async Task ReportsErrorWhenUpgradeMethodReturnsTask(string returnDeclaration, string body) {
		string source = $$"""
		                  using Cat.Network;
		                  using System.Threading.Tasks;

		                  [NetworkObjectAttribute(Version = 1)]
		                  public sealed partial class Player : NetworkObject {
		                      [UpgradeTo(1)]
		                      private static {{returnDeclaration}} UpgradeToVersion1(NetworkObjectUpgradeReader reader, NetworkObjectUpgradeWriter writer) {
		                          {{body}}
		                      }
		                  }
		                  """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		AssertInvalidUpgradeSignature(diagnostics);
	}

	[Test]
	public async Task ReportsErrorWhenUpgradeToAttributeIsUsedOutsideNetworkObjectType() {
		const string source = """
		                      using Cat.Network;

		                      public sealed class Player {
		                      	[UpgradeTo(1)]
		                      	private static void UpgradeToVersion1(NetworkObjectUpgradeReader reader, NetworkObjectUpgradeWriter writer) {
		                      	}
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Has.Length.EqualTo(1));
		Assert.Multiple(() => {
			Assert.That(diagnostics[0].Id, Is.EqualTo("CN0017"));
			Assert.That(diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(diagnostics[0].GetMessage(), Does.Contain("UpgradeToVersion1"));
		});
	}

	[Test]
	public async Task ReportsErrorWhenUpgradeMethodIsNotStatic() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObjectAttribute(Version = 1)]
		                      public sealed partial class Player : NetworkObject {
		                      	[UpgradeTo(1)]
		                      	private void UpgradeToVersion1(NetworkObjectUpgradeReader reader, NetworkObjectUpgradeWriter writer) {
		                      	}
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Has.Length.EqualTo(1));
		Assert.Multiple(() => {
			Assert.That(diagnostics[0].Id, Is.EqualTo("CN0018"));
			Assert.That(diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(diagnostics[0].GetMessage(), Does.Contain("UpgradeToVersion1"));
		});
	}

	[Test]
	public async Task ReportsErrorWhenUpgradeMethodHasWrongParameters() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObjectAttribute(Version = 1)]
		                      public sealed partial class Player : NetworkObject {
		                      	[UpgradeTo(1)]
		                      	private static void UpgradeToVersion1(NetworkObjectUpgradeWriter writer, NetworkObjectUpgradeReader reader) {
		                      	}
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Has.Length.EqualTo(1));
		Assert.Multiple(() => {
			Assert.That(diagnostics[0].Id, Is.EqualTo("CN0018"));
			Assert.That(diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(diagnostics[0].GetMessage(), Does.Contain("UpgradeToVersion1"));
		});
	}

	[Test]
	public async Task ReportsErrorWhenUpgradeMethodIsGenericOrReturnsValue() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObjectAttribute(Version = 2)]
		                      public sealed partial class Player : NetworkObject {
		                      	[UpgradeTo(1)]
		                      	private static int UpgradeToVersion1(NetworkObjectUpgradeReader reader, NetworkObjectUpgradeWriter writer) {
		                      		return 0;
		                      	}

		                      	[UpgradeTo(2)]
		                      	private static void UpgradeToVersion2<T>(NetworkObjectUpgradeReader reader, NetworkObjectUpgradeWriter writer) {
		                      	}
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EquivalentTo(new[] { "CN0018", "CN0018" }));
	}

	[Test]
	public async Task ReportsErrorWhenUpgradeTargetVersionIsZero() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObjectAttribute(Version = 1)]
		                      public sealed partial class Player : NetworkObject {
		                      	[UpgradeTo(0)]
		                      	private static void UpgradeToVersion0(NetworkObjectUpgradeReader reader, NetworkObjectUpgradeWriter writer) {
		                      	}
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Has.Length.EqualTo(1));
		Assert.Multiple(() => {
			Assert.That(diagnostics[0].Id, Is.EqualTo("CN0020"));
			Assert.That(diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(diagnostics[0].GetMessage(), Does.Contain("version 0"));
			Assert.That(diagnostics[0].GetMessage(), Does.Contain("schema version 1"));
		});
	}

	[Test]
	public async Task ReportsErrorWhenUpgradeTargetVersionExceedsNetworkObjectVersion() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObjectAttribute(Version = 1)]
		                      public sealed partial class Player : NetworkObject {
		                      	[UpgradeTo(2)]
		                      	private static void UpgradeToVersion2(NetworkObjectUpgradeReader reader, NetworkObjectUpgradeWriter writer) {
		                      	}
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Has.Length.EqualTo(1));
		Assert.Multiple(() => {
			Assert.That(diagnostics[0].Id, Is.EqualTo("CN0020"));
			Assert.That(diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(diagnostics[0].GetMessage(), Does.Contain("version 2"));
			Assert.That(diagnostics[0].GetMessage(), Does.Contain("schema version 1"));
		});
	}

	[Test]
	public async Task ReportsErrorWhenMultipleUpgradeMethodsTargetSameVersion() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObjectAttribute(Version = 2)]
		                      public sealed partial class Player : NetworkObject {
		                      	[UpgradeTo(2)]
		                      	private static void UpgradeToVersion2A(NetworkObjectUpgradeReader reader, NetworkObjectUpgradeWriter writer) {
		                      	}

		                      	[UpgradeTo(2)]
		                      	private static void UpgradeToVersion2B(NetworkObjectUpgradeReader reader, NetworkObjectUpgradeWriter writer) {
		                      	}
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EquivalentTo(new[] { "CN0019", "CN0019" }));
		Assert.That(diagnostics.Select(static diagnostic => diagnostic.GetMessage()), Has.All.Contains("version 2"));
	}

	private static void AssertInvalidUpgradeSignature(ImmutableArray<Diagnostic> diagnostics) {
		Assert.That(diagnostics, Has.Length.EqualTo(1));
		Assert.Multiple(() => {
			Assert.That(diagnostics[0].Id, Is.EqualTo("CN0018"));
			Assert.That(diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(diagnostics[0].GetMessage(), Does.Contain("UpgradeToVersion1"));
			Assert.That(diagnostics[0].Location.SourceTree!.GetText().ToString(diagnostics[0].Location.SourceSpan), Is.EqualTo("UpgradeToVersion1"));
			Assert.That(diagnostics[0].GetMessage(), Does.Contain("non-async"));
		});
	}
}
