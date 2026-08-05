using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Cat.Network.Analyzer.Test;

public sealed class NetworkMessageAttributeAnalyzerTests {
	[Test]
	public async Task ReportsErrorWhenRpcIsDeclaredOutsideNetworkEntity() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObject]
		                      public sealed partial class Payload : NetworkObject {
		                      	[RPC]
		                      	public partial void Ping(int value);
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics.Single().Id, Is.EqualTo("CN0021"));
	}

	[Test]
	public async Task ReportsErrorWhenRpcIsNotPartialVoid() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkEntity {
		                      	[RPC]
		                      	public int Ping(int value) => value;
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics.Single().Id, Is.EqualTo("CN0022"));
	}

	[Test]
	public async Task ReportsErrorWhenBroadcastUsesNetworkEntityParameter() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkEntity {
		                      	[Broadcast]
		                      	public partial void Ping(Player other);
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics.Single().Id, Is.EqualTo("CN0024"));
	}

	[Test]
	public async Task ReportsErrorWhenRpcUsesUnsupportedParameter() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkEntity {
		                      	[RPC]
		                      	public partial void Ping(object value);
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics.Single().Id, Is.EqualTo("CN0023"));
	}

	[Test]
	public async Task DoesNotReportErrorWhenRpcUsesSupportedNetworkObjectParameter() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObject]
		                      public sealed partial class Payload : NetworkObject {
		                      	[NetworkProperty]
		                      	public partial int Value { get; set; }
		                      }

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkEntity {
		                      	[RPC]
		                      	public partial void Ping(Payload value, int count);
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Is.Empty);
	}

	[Test]
	public async Task ReportsWarningWhenExplicitRpcHandlerIsPublic() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkEntity {
		                      	[RPC(NetworkMessageReceiveMode.Explicit)]
		                      	public partial void RequestOwnership(int value);

		                      	public void RequestOwnership(RelayClient client, NetworkProfile instigator, int value) {
		                      	}
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics.Single().Id, Is.EqualTo("CN0025"));
	}

	[Test]
	public async Task DoesNotReportWarningWhenExplicitRpcHandlerUsesExplicitInterfaceImplementation() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkEntity {
		                      	public partial interface RPC {
		                      		void RequestOwnership(RelayClient client, NetworkProfile instigator, int value);
		                      	}

		                      	[RPC(NetworkMessageReceiveMode.Explicit)]
		                      	public partial void RequestOwnership(int value);

		                      	void RPC.RequestOwnership(RelayClient client, NetworkProfile instigator, int value) {
		                      	}
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Is.Empty);
	}

	[Test]
	public async Task DoesNotReportWarningForPublicEventModeHandler() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkEntity {
		                      	[RPC]
		                      	public partial void RequestOwnership(int value);

		                      	public void RequestOwnership(RelayClient client, NetworkProfile instigator, int value) {
		                      	}
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Is.Empty);
	}
}
