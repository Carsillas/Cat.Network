using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Cat.Network.Analyzer.Test;

public sealed class NetworkCollectionAttributeAnalyzerTests {
	[Test]
	public async Task ReportsErrorWhenNetworkCollectionAttributeIsUsedInNonNetworkObjectSubclass() {
		const string source = """
		                      using Cat.Network;

		                      public sealed partial class Player {
		                      	[NetworkCollection]
		                      	public partial NetworkList<int> Scores { get; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { "CN0010" }));
	}

	[Test]
	public async Task ReportsErrorWhenNetworkCollectionAttributeIsUsedOnNonPartialProperty() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkCollection]
		                      	public NetworkList<int> Scores { get; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { "CN0011" }));
	}

	[Test]
	public async Task ReportsErrorWhenNetworkCollectionAttributeIsUsedOnPropertyWithSetter() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkCollection]
		                      	public partial NetworkList<int> Scores { get; set; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { "CN0012" }));
	}

	[Test]
	public async Task ReportsErrorWhenNetworkCollectionAttributeIsUsedOnNonNetworkCollectionProperty() {
		const string source = """
		                      using System.Collections.Generic;
		                      using Cat.Network;

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkCollection]
		                      	public partial IList<int> Scores { get; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { "CN0013" }));
	}

	[Test]
	public async Task ReportsErrorWhenNetworkCollectionAttributePropertyDeclaresInitializer() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkCollection]
		                      	public partial NetworkList<int> Scores { get; } = null!;
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { "CN0014" }));
	}

	[Test]
	public async Task ReportsErrorWhenNetworkCollectionAttributeUsesUnsupportedItemType() {
		const string source = """
		                      using Cat.Network;

		                      public class UnsupportedType {
		                      }

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkCollection]
		                      	public partial NetworkList<UnsupportedType> Scores { get; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { "CN0015" }));
	}

	[Test]
	public async Task ReportsErrorWhenNetworkCollectionAttributeUsesUnsupportedDictionaryKeyType() {
		const string source = """
		                      using Cat.Network;

		                      public class UnsupportedKey {
		                      }

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkCollection]
		                      	public partial NetworkDictionary<UnsupportedKey, int> Scores { get; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { "CN0016" }));
	}

	[Test]
	public async Task ReportsErrorWhenNetworkListPropertyIsNotMarkedWithNetworkCollectionAttribute() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkObject {
		                      	public partial NetworkList<int> Scores { get; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { "CN0026" }));
	}

	[Test]
	public async Task ReportsErrorWhenNetworkDictionaryPropertyIsNotMarkedWithNetworkCollectionAttribute() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkObject {
		                      	public partial NetworkDictionary<int, string> Scores { get; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { "CN0026" }));
	}

	[Test]
	public async Task DoesNotReportErrorWhenNetworkCollectionAttributeUsesSupportedStructItemType() {
		const string source = """
		                      using System;
		                      using Cat.Network;

		                      public struct ScoreEntry {
		                      	public int Score;
		                      	public Guid SessionId;
		                      }

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkCollection]
		                      	public partial NetworkList<ScoreEntry> Scores { get; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Is.Empty);
	}

	[Test]
	public async Task DoesNotReportErrorWhenNetworkCollectionAttributeUsesNetworkObjectItemType() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObject]
		                      public partial class Child : NetworkObject {
		                      }

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkCollection]
		                      	public partial NetworkList<Child> Scores { get; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Is.Empty);
	}

	[Test]
	public async Task DoesNotReportErrorWhenNetworkCollectionAttributeIsUsedOnGetterOnlyPartialNetworkListPropertyWithoutInitializer() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkCollection]
		                      	public partial NetworkList<int> Scores { get; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Is.Empty);
	}

	[Test]
	public async Task DoesNotReportErrorWhenNetworkCollectionAttributeIsUsedOnGetterOnlyPartialNetworkDictionaryPropertyWithoutInitializer() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkCollection]
		                      	public partial NetworkDictionary<int, string> Scores { get; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Is.Empty);
	}

	[Test]
	public async Task DoesNotReportErrorWhenNetworkCollectionAttributeIsUsedOnPrivateGetterOnlyPartialNetworkListPropertyWithoutInitializer() {
		const string source = """
		                      using Cat.Network;

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkCollection]
		                      	private partial NetworkList<int> Scores { get; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Is.Empty);
	}

	[Test]
	public async Task DoesNotReportErrorWhenNetworkListPropertyInNonNetworkObjectTypeIsNotMarkedWithNetworkCollectionAttribute() {
		const string source = """
		                      using Cat.Network;

		                      public sealed partial class Player {
		                      	public partial NetworkList<int> Scores { get; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Is.Empty);
	}
}
