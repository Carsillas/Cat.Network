using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Cat.Network.Analyzer.Test;

public sealed class NetworkObjectAttributeAnalyzerTests {
	[Test]
	public async Task ReportsErrorWhenNetworkObjectSubclassIsMissingAttribute() {
		const string source = """
		                      using Cat.Network;

		                      public sealed class Player : NetworkObject
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
	public async Task DoesNotReportErrorWhenNetworkObjectSubclassHasAttribute() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObjectAttribute]
		                      public sealed partial class Player : NetworkObject
		                      {
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Is.Empty);
	}

	[Test]
	public async Task ReportsErrorWhenNetworkObjectSubclassWithAttributeIsNotPartial() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObjectAttribute]
		                      public sealed class Player : NetworkObject
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
	public async Task DoesNotReportErrorWhenClassHasNoNetworkObjectInheritanceOrAttribute() {
		const string source = """
		                      public sealed class Player
		                      {
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Is.Empty);
	}

	[Test]
	public async Task ReportsErrorWhenNetworkObjectAttributeIsUsedOnNonNetworkObjectSubclass() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObjectAttribute]
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
