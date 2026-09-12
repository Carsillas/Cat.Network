using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Cat.Network.Analyzer.Test;

public sealed class NetworkCollectionSymbolTypeAnalyzerTests {
	[TestCase("NetworkList<int[]>", "int[]", "CN0015")]
	[TestCase("NetworkList<int[,]>", "int[,]", "CN0015")]
	[TestCase("NetworkList<int[][]>", "int[][]", "CN0015")]
	[TestCase("NetworkList<dynamic>", "dynamic", "CN0015")]
	[TestCase("NetworkDictionary<int, int[]>", "int[]", "CN0015")]
	[TestCase("NetworkDictionary<int, dynamic>", "dynamic", "CN0015")]
	[TestCase("NetworkDictionary<int[], int>", "int[]", "CN0016")]
	[TestCase("NetworkDictionary<dynamic, int>", "dynamic", "CN0016")]
	public async Task ReportsUnsupportedNonNamedTypeWithoutAnalyzerException(string collectionType, string unsupportedType, string diagnosticId) {
		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(CreateSource(collectionType));

		AssertUnsupportedDiagnostics(diagnostics, diagnosticId);
		Assert.That(diagnostics.Single().GetMessage(), Does.Contain($"'{unsupportedType}'"));
	}

	[TestCase("NetworkList<T>", "", "CN0015")]
	[TestCase("NetworkList<T>", "where T : struct", "CN0015")]
	[TestCase("NetworkList<T>", "where T : NetworkObject", "CN0015")]
	[TestCase("NetworkDictionary<int, T>", "", "CN0015")]
	[TestCase("NetworkDictionary<T, int>", "where T : notnull", "CN0016")]
	[TestCase("NetworkList<Box<T>>", "", "CN0015")]
	[TestCase("NetworkDictionary<int, Box<T>>", "", "CN0015")]
	[TestCase("NetworkDictionary<Box<T>, int>", "", "CN0016")]
	public async Task ReportsUnsupportedTypeParameterWithoutAnalyzerException(string collectionType, string constraints, string diagnosticId) {
		string source = CreateSource(collectionType, "public struct Box<T> { public T Value; }", "<T>", constraints);
		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(source);

		// Generic network declarations have separate validation and generation restrictions.
		Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id), Does.Not.Contain("AD0001"));
		ImmutableArray<Diagnostic> collectionDiagnostics = diagnostics
			.Where(static diagnostic => diagnostic.Id is "CN0015" or "CN0016")
			.ToImmutableArray();
		AssertUnsupportedDiagnostics(collectionDiagnostics, diagnosticId);
	}

	[TestCase("NetworkList<Entry>", "int[]", "CN0015")]
	[TestCase("NetworkList<Entry>", "Box<int[]>", "CN0015")]
	[TestCase("NetworkList<Entry>", "Box<Box<int[]>>", "CN0015")]
	[TestCase("NetworkDictionary<int, Entry>", "int[]", "CN0015")]
	[TestCase("NetworkDictionary<int, Entry>", "Box<int[]>", "CN0015")]
	[TestCase("NetworkDictionary<int, Entry>", "Box<Box<int[]>>", "CN0015")]
	[TestCase("NetworkDictionary<Entry, int>", "int[]", "CN0016")]
	[TestCase("NetworkDictionary<Entry, int>", "Box<int[]>", "CN0016")]
	[TestCase("NetworkDictionary<Entry, int>", "Box<Box<int[]>>", "CN0016")]
	public async Task ReportsNestedUnsupportedArrayWithoutAnalyzerException(string collectionType, string fieldType, string diagnosticId) {
		string declarations = $$"""
		                        public struct Box<T> { public T Value; }
		                        public struct Entry { public {{fieldType}} Value; }
		                        """;
		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(CreateSource(collectionType, declarations));

		AssertUnsupportedDiagnostics(diagnostics, diagnosticId);
	}

	[Test]
	public async Task ReportsBothUnsupportedKeyAndValueWithoutAnalyzerException() {
		ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHost.GetAnalyzerDiagnosticsAsync(CreateSource("NetworkDictionary<int[], int[]>"));

		AssertUnsupportedDiagnostics(diagnostics, "CN0015", "CN0016");
	}

	private static void AssertUnsupportedDiagnostics(ImmutableArray<Diagnostic> diagnostics, params string[] expectedIds) {
		Assert.Multiple(() => {
			Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id).Order(), Is.EqualTo(expectedIds));
			Assert.That(diagnostics.All(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.True);
			foreach (Diagnostic diagnostic in diagnostics) {
				Assert.That(diagnostic.Location.SourceTree?.GetText().ToString(diagnostic.Location.SourceSpan), Is.EqualTo("Items"));
			}
		});
	}

	private static string CreateSource(string collectionType, string declarations = "", string typeParameters = "", string constraints = "") {
		return $$"""
		         using Cat.Network;

		         {{declarations}}

		         [NetworkObject]
		         public partial class Player{{typeParameters}} : NetworkObject {{constraints}} {
		             [NetworkCollection]
		             public partial {{collectionType}} Items { get; }
		         }
		         """;
	}
}
