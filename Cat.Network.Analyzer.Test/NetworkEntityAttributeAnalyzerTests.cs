using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Cat.Network.Analyzer.Test;

public sealed class NetworkEntityAttributeAnalyzerTests {
	[Test]
	public async Task ReportsErrorWhenNetworkEntitySubclassIsMissingAttribute() {
		const string source = """
		                      using Cat.Network;

		                      public sealed class Player : NetworkEntity
		                      {
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

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

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

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

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

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

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

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

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Has.Length.EqualTo(1));
		Assert.Multiple(() => {
			Assert.That(diagnostics[0].Id, Is.EqualTo("CN0002"));
			Assert.That(diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(diagnostics[0].GetMessage(), Does.Contain("Player"));
		});
	}
}
