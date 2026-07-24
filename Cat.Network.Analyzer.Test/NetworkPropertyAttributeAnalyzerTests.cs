using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Cat.Network.Analyzer.Test;

public sealed class NetworkPropertyAttributeAnalyzerTests {
	[Test]
	public async Task ReportsErrorWhenNetworkPropertyAttributeIsUsedInNonNetworkObjectSubclass() {
		const string source = """
		                      using Cat.Network;

		                      public sealed partial class Player {
		                      	[NetworkProperty]
		                      	public partial int Health { get; set; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Has.Length.EqualTo(1));
		Assert.Multiple(() => {
			Assert.That(diagnostics[0].Id, Is.EqualTo("CN0004"));
			Assert.That(diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(diagnostics[0].GetMessage(), Does.Contain("Health"));
		});
	}

	[Test]
	public async Task DoesNotReportErrorWhenNetworkPropertyAttributeIsUsedInNetworkObjectSubclass() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObjectAttribute]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkProperty]
		                      	public partial int Health { get; set; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Is.Empty);
	}

	[Test]
	public async Task ReportsErrorWhenNetworkPropertyAttributeIsUsedOnNonPartialProperty() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObjectAttribute]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkProperty]
		                      	public int Health { get; set; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Has.Length.EqualTo(1));
		Assert.Multiple(() => {
			Assert.That(diagnostics[0].Id, Is.EqualTo("CN0005"));
			Assert.That(diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(diagnostics[0].GetMessage(), Does.Contain("Health"));
		});
	}

	[Test]
	public async Task ReportsErrorWhenNetworkPropertyAttributeIsUsedOnGetterOnlyProperty() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObjectAttribute]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkProperty]
		                      	public partial int Health { get; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Has.Length.EqualTo(1));
		Assert.Multiple(() => {
			Assert.That(diagnostics[0].Id, Is.EqualTo("CN0006"));
			Assert.That(diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(diagnostics[0].GetMessage(), Does.Contain("Health"));
		});
	}

	[Test]
	public async Task ReportsErrorWhenNetworkPropertyAttributeIsUsedOnSetterOnlyProperty() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObjectAttribute]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkProperty]
		                      	public partial int Health { set { } }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Has.Length.EqualTo(1));
		Assert.Multiple(() => {
			Assert.That(diagnostics[0].Id, Is.EqualTo("CN0006"));
			Assert.That(diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(diagnostics[0].GetMessage(), Does.Contain("Health"));
		});
	}

	[Test]
	public async Task ReportsErrorWhenNetworkPropertyHidesInheritedNetworkProperty() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObjectAttribute]
		                      public partial class Actor : NetworkObject {
		                      	[NetworkProperty]
		                      	public partial int Health { get; set; }
		                      }

		                      [NetworkObjectAttribute]
		                      public sealed partial class Player : Actor {
		                      	[NetworkProperty]
		                      	public new partial int Health { get; set; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Has.Length.EqualTo(1));
		Assert.Multiple(() => {
			Assert.That(diagnostics[0].Id, Is.EqualTo("CN0007"));
			Assert.That(diagnostics[0].Severity, Is.EqualTo(DiagnosticSeverity.Error));
			Assert.That(diagnostics[0].Location.GetLineSpan().StartLinePosition.Line, Is.EqualTo(11));
			Assert.That(diagnostics[0].GetMessage(), Does.Contain("Health"));
			Assert.That(diagnostics[0].GetMessage(), Does.Contain("Actor"));
		});
	}
}
