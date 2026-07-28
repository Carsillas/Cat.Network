using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Cat.Network.Analyzer.Test;

public sealed class NetworkCollectionAttributeAnalyzerTests {
	[Test]
	public async Task ReportsErrorWhenNetworkCollectionAttributeIsUsedInNonNetworkObjectSubclass() {
		const string source = """
		                      using System.Collections.Generic;
		                      using Cat.Network;

		                      public sealed partial class Player {
		                      	[NetworkCollection]
		                      	public partial IList<int> Scores { get; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { "CN0010" }));
	}

	[Test]
	public async Task ReportsErrorWhenNetworkCollectionAttributeIsUsedOnNonPartialProperty() {
		const string source = """
		                      using System.Collections.Generic;
		                      using Cat.Network;

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkCollection]
		                      	public IList<int> Scores { get; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { "CN0011" }));
	}

	[Test]
	public async Task ReportsErrorWhenNetworkCollectionAttributeIsUsedOnPropertyWithSetter() {
		const string source = """
		                      using System.Collections.Generic;
		                      using Cat.Network;

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkCollection]
		                      	public partial IList<int> Scores { get; set; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { "CN0012" }));
	}

	[Test]
	public async Task ReportsErrorWhenNetworkCollectionAttributeIsUsedOnNonIListProperty() {
		const string source = """
		                      using System.Collections.Generic;
		                      using Cat.Network;

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkCollection]
		                      	public partial List<int> Scores { get; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { "CN0013" }));
	}

	[Test]
	public async Task ReportsErrorWhenNetworkCollectionAttributePropertyDeclaresInitializer() {
		const string source = """
		                      using System.Collections.Generic;
		                      using Cat.Network;

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkCollection]
		                      	public partial IList<int> Scores { get; } = [];
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { "CN0014" }));
	}

	[Test]
	public async Task ReportsErrorWhenNetworkCollectionAttributeUsesUnsupportedItemType() {
		const string source = """
		                      using System.Collections.Generic;
		                      using Cat.Network;

		                      public class UnsupportedType {
		                      }

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkCollection]
		                      	public partial IList<UnsupportedType> Scores { get; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { "CN0015" }));
	}

	[Test]
	public async Task ReportsErrorWhenNetworkCollectionAttributeUsesUnsupportedDictionaryKeyType() {
		const string source = """
		                      using System.Collections.Generic;
		                      using Cat.Network;

		                      public class UnsupportedKey {
		                      }

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkCollection]
		                      	public partial IDictionary<UnsupportedKey, int> Scores { get; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Is.EqualTo(new[] { "CN0016" }));
	}

	[Test]
	public async Task DoesNotReportErrorWhenNetworkCollectionAttributeUsesSupportedStructItemType() {
		const string source = """
		                      using System;
		                      using System.Collections.Generic;
		                      using Cat.Network;

		                      public struct ScoreEntry {
		                      	public int Score;
		                      	public Guid SessionId;
		                      }

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkCollection]
		                      	public partial IList<ScoreEntry> Scores { get; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Is.Empty);
	}

	[Test]
	public async Task DoesNotReportErrorWhenNetworkCollectionAttributeUsesNetworkObjectItemType() {
		const string source = """
		                      using System.Collections.Generic;
		                      using Cat.Network;

		                      [NetworkObject]
		                      public partial class Child : NetworkObject {
		                      }

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkCollection]
		                      	public partial IList<Child> Scores { get; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Is.Empty);
	}

	[Test]
	public async Task DoesNotReportErrorWhenNetworkCollectionAttributeIsUsedOnGetterOnlyPartialIListPropertyWithoutInitializer() {
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

		Assert.That(diagnostics, Is.Empty);
	}

	[Test]
	public async Task DoesNotReportErrorWhenNetworkCollectionAttributeIsUsedOnGetterOnlyPartialIDictionaryPropertyWithoutInitializer() {
		const string source = """
		                      using System.Collections.Generic;
		                      using Cat.Network;

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkCollection]
		                      	public partial IDictionary<int, string> Scores { get; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Is.Empty);
	}

	[Test]
	public async Task DoesNotReportErrorWhenNetworkCollectionAttributeIsUsedOnPrivateGetterOnlyPartialIListPropertyWithoutInitializer() {
		const string source = """
		                      using System.Collections.Generic;
		                      using Cat.Network;

		                      [NetworkObject]
		                      public sealed partial class Player : NetworkObject {
		                      	[NetworkCollection]
		                      	private partial IList<int> Scores { get; }
		                      }
		                      """;

		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		Assert.That(diagnostics, Is.Empty);
	}
}
