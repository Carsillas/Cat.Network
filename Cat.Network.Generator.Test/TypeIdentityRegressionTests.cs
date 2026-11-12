using System.Collections.Immutable;
using System.Reflection;
using Cat.Network.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cat.Network.Generator.Test;

public sealed class TypeIdentityRegressionTests {
	[Test]
	public async Task NamespaceAndOutputRoleCollisionsKeepEveryTypeAndSerializer() {
		const string source = """
			namespace A { [NetworkObject] public partial class B_C : NetworkObject {} }
			namespace A.B { [NetworkObject] public partial class C : NetworkObject {} }
			namespace A_ { [NetworkObject] public partial class B : NetworkObject {} }
			namespace A { [NetworkObject] public partial class _B : NetworkObject {} }
			[NetworkObject] public partial class Entity : NetworkEntity { [RPC] public partial void Ping(int amount); }
			[NetworkObject] public partial class Entity_Serializer : NetworkObject {}
			[NetworkObject] public partial class Entity_Messages : NetworkObject {}
			[NetworkObject] public partial class Case : NetworkObject {}
			[NetworkObject] public partial class @case : NetworkObject {}
			public static class Probe {
				public static int Run() {
					var catalogue = new TypeCatalogue();
					Type[] types = { typeof(A.B_C), typeof(A.B.C), typeof(A_.B), typeof(A._B), typeof(Entity), typeof(Entity_Serializer), typeof(Entity_Messages), typeof(Case), typeof(@case) };
					foreach (Type type in types) {
						catalogue.Register(type);
						if (!catalogue.TryFindTypeId(type, out var id) || !catalogue.TryFindType(id, out var found) || found != type) return -1;
						if (!catalogue.TryFindSerializer(type, out var serializer)) return -2;
						var writer = new BufferWriter();
						serializer.Serialize(writer, (NetworkObject)Activator.CreateInstance(type)!, new(catalogue), new(MemberSelectionMode.All, MemberIdentificationMode.Name));
						serializer.Deserialize((NetworkObject)Activator.CreateInstance(type)!, writer.GetWrittenSpan(), new(catalogue));
					}
					return types.Length;
				}
			}
			""";

		RunResult result = await RunAsync(source);
		AssertCompiles(result);
		string[] hints = result.GeneratorResult.Results.Single().GeneratedSources.Select(item => item.HintName).ToArray();
		Assert.That(hints.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(19));
		Assert.That(Execute(result), Is.EqualTo(9));
	}

	[Test]
	public async Task LongTypeNamesKeepEveryHintPathComponentWithinFileSystemLimits() {
		string namespaceName = "CompanyWithALongNamespace.ProductWithALongNamespace.FeatureWithALongNamespace";
		string typeName = "Entity" + new string('A', 160);
		RunResult result = await RunAsync($$"""
			namespace {{namespaceName}} {
				[NetworkObject] public partial class {{typeName}} : NetworkObject {}
				[NetworkObject] public partial class {{typeName}}B : NetworkObject {}
			}
			""");
		AssertCompiles(result);
		string[] hints = result.GeneratorResult.Results.Single().GeneratedSources.Select(item => item.HintName).ToArray();
		Assert.Multiple(() => {
			Assert.That(hints.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(4));
			Assert.That(hints.SelectMany(hint => hint.Split('/', '\\')).All(component => component.Length <= 128), Is.True);
		});
	}

	[TestCase("[NetworkObject] public partial class Bad<T> : NetworkObject where T : class, new() { [NetworkProperty] public partial int Value { get; set; } }", "CN0035")]
	[TestCase("[NetworkObject] public abstract partial class Bad<T> : NetworkObject {}", "CN0035")]
	[TestCase("[NetworkObject] public abstract partial class Base<T> : NetworkObject {} [NetworkObject] public partial class Bad : Base<int> {}", "CN0035")]
	[TestCase("public partial class Outer<T> { [NetworkObject] public partial class Bad : NetworkObject {} }", "CN0035")]
	[TestCase("public partial class Outer { [NetworkObject] public partial class Bad : NetworkObject { [NetworkProperty] public partial int Value { get; set; } } }", "CN0036")]
	[TestCase("public class Outer { [NetworkObject] private partial class Bad : NetworkObject {} }", "CN0036")]
	[TestCase("file partial class Bad : NetworkObject { public Bad() {} public override NetworkObject Clone() => this; }", "CN0037")]
	public async Task UnsupportedTypeScopeIsDiagnosedBeforeEmission(string declaration, string diagnosticId) {
		if (diagnosticId == "CN0037") declaration = "[NetworkObject] " + declaration;
		await AssertRejectedAsync(declaration, diagnosticId);
	}

	[TestCase("[NetworkProperty] private partial Stat Value { get; set; }")]
	[TestCase("[NetworkProperty] private partial Stat? Value { get; set; }")]
	[TestCase("[NetworkCollection] private partial NetworkList<Stat> Values { get; }")]
	[TestCase("[NetworkCollection] private partial NetworkDictionary<int, Stat> Values { get; }")]
	[TestCase("[NetworkCollection] private partial NetworkDictionary<Stat, int> Values { get; }")]
	[TestCase("[RPC] private partial void Send(Stat value);")]
	[TestCase("[Broadcast] private partial void Send(Stat value);")]
	[TestCase("[RPC(NetworkMessageReceiveMode.Explicit)] private partial void Send(Stat value);")]
	public async Task PrivateSerializedTypesAreDiagnosedForEveryMemberPath(string member) {
		await AssertRejectedAsync($$"""
			[NetworkObject] public partial class Bad : NetworkEntity {
				private struct Stat { public int Amount; }
				{{member}}
			}
			""", "CN0038");
	}

	[TestCase("private class Container { public struct Stat { public int Amount; } }", "Container.Stat")]
	[TestCase("private struct Stat { public int Amount; } public struct Box<T> { public int Amount; }", "Box<Stat>")]
	[TestCase("protected struct Stat { public int Amount; }", "Stat")]
	[TestCase("private protected struct Stat { public int Amount; }", "Stat")]
	public async Task ContainingTypesAndGenericArgumentsMustAlsoBeAccessible(string types, string propertyType) {
		await AssertRejectedAsync($$"""
			[NetworkObject] public partial class Bad : NetworkObject {
				{{types}}
				[NetworkProperty] private partial {{propertyType}} Value { get; set; }
			}
			""", "CN0038");
	}

	[TestCase("RPC")]
	[TestCase("Broadcast")]
	[TestCase("RPC(NetworkMessageReceiveMode.Explicit)")]
	public async Task PublicMessageReceiveApiRequiresPublicParameterTypes(string attribute) {
		await AssertRejectedAsync($$"""
			internal struct Stat { public int Amount; }
			[NetworkObject] public partial class Bad : NetworkEntity {
				[{{attribute}}] private partial void Send(Stat value);
			}
			""", "CN0039");
	}

	[Test]
	public async Task EscapedTypeNamesAndAccessibleNestedStructFieldsCompileAndRoundTrip() {
		const string source = """
			namespace @namespace {
				public class Data {
					public struct @struct { public int Amount; }
					public struct Stats { public @struct Details; }
				}
				[NetworkObject] public partial class @class : NetworkEntity {
					[NetworkProperty] public partial Data.Stats Value { get; set; }
					[NetworkCollection] public partial NetworkList<Data.Stats> Values { get; }
					[NetworkCollection] public partial NetworkDictionary<Data.@struct, Data.Stats> Lookup { get; }
					[RPC] public partial void Send(Data.Stats value);
				}
				[NetworkObject] public partial class Profile : NetworkProfile {}
			}
			public static class Probe {
				public static int Run() {
					var catalogue = new TypeCatalogue(); catalogue.Register(typeof(@namespace.@class)); catalogue.Register(typeof(@namespace.Profile));
					var value = new @namespace.Data.Stats { Details = new() { Amount = 42 } };
					var source = new @namespace.@class { Value = value }; source.Values.Add(value); source.Lookup.Add(value.Details, value);
					catalogue.TryFindSerializer(source.GetType(), out var serializer);
					var writer = new BufferWriter(); serializer!.Serialize(writer, source, new(catalogue), new(MemberSelectionMode.All, MemberIdentificationMode.Name));
					var target = new @namespace.@class(); serializer.Deserialize(target, writer.GetWrittenSpan(), new(catalogue));
					int received = 0; target.SendReceived += (_, _, item) => received = item.Details.Amount;
					var parameters = new BufferWriter(); parameters.WriteInt32(4); parameters.WriteInt32(42);
					ulong id = BitConverter.ToUInt64(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("Send(global::namespace.Data.Stats)")));
					if (!((INetworkRpcTarget)target).TryInvokeRpc(new(catalogue), new @namespace.Profile(), id, parameters.GetWrittenSpan(), new(catalogue))) return -1;
					return target.Value.Details.Amount + target.Values[0].Details.Amount + target.Lookup[value.Details].Details.Amount + received + source.Clone().Value.Details.Amount;
				}
			}
			""";

		RunResult result = await RunAsync(source);
		AssertCompiles(result);
		Assert.That(Execute(result), Is.EqualTo(210));
	}

	[Test]
	public async Task PrivateInheritedMembersWithAccessibleTypesRemainSupported() {
		const string source = """
			[NetworkObject] public abstract partial class Base : NetworkObject {
				protected internal struct Stat { public int Amount; }
				[NetworkProperty] private partial Stat Value { get; set; }
				[NetworkCollection] private partial NetworkList<int> Values { get; }
				public void SetValue(int amount) { Value = new() { Amount = amount }; Values.Add(amount); }
				public int Read() => Value.Amount;
				public int Count() => Values.Count;
			}
			[NetworkObject] internal partial class Derived : Base { [NetworkProperty] public partial int Other { get; set; } }
			public static class Probe {
				public static int Run() {
					var catalogue = new TypeCatalogue(); catalogue.Register(typeof(Derived));
					var source = new Derived { Other = 4 }; source.SetValue(42);
					catalogue.TryFindSerializer(typeof(Derived), out var serializer);
					var writer = new BufferWriter(); serializer!.Serialize(writer, source, new(catalogue), new(MemberSelectionMode.All, MemberIdentificationMode.Name));
					var target = new Derived(); serializer.Deserialize(target, writer.GetWrittenSpan(), new(catalogue));
					return target.Read() + target.Count() + target.Other + source.Clone().Read();
				}
			}
			""";

		RunResult result = await RunAsync(source);
		AssertCompiles(result);
		Assert.That(Execute(result), Is.EqualTo(89));
	}

	[Test]
	public async Task InternalMessageOwnerCanExposeInternalDataTypes() {
		RunResult result = await RunAsync("""
			internal class Container { public struct Stat { public int Amount; } }
			[NetworkObject] internal partial class Entity : NetworkEntity {
				[RPC] public partial void Send(Container.Stat value);
			}
			""");
		AssertCompiles(result);
	}

	[Test]
	public async Task ClosedGenericDataStructsRemainSupported() {
		RunResult result = await RunAsync("""
			public struct Box<T> { public T Item; }
			public class Container { public struct Stat { public int Amount; } }
			[NetworkObject] public partial class Entity : NetworkEntity {
				[NetworkProperty] public partial Box<Container.Stat> Value { get; set; }
				[NetworkCollection] public partial NetworkList<Box<Container.Stat>> Values { get; }
				[RPC] public partial void Send(Box<Container.Stat> value);
			}
			""");
		AssertCompiles(result);
	}

	private static async Task AssertRejectedAsync(string declaration, string diagnosticId) {
		RunResult result = await RunAsync(declaration + """

			[NetworkObject] public partial class Good : NetworkObject {
				[NetworkProperty] public partial int Value { get; set; }
			}
			""");
		Assert.Multiple(() => {
			Assert.That(result.InputAnalyzerDiagnostics.Concat(result.OutputAnalyzerDiagnostics).Select(item => item.Id), Does.Not.Contain(diagnosticId), "Emission diagnostics belong to the generator and must not be reported again by the analyzer.");
			Assert.That(result.GeneratorDiagnostics.Select(item => item.Id), Does.Contain(diagnosticId));
			Assert.That(result.GeneratorDiagnostics.Where(item => item.Id == diagnosticId).All(item => item.Location.IsInSource), Is.True);
			Assert.That(result.GeneratorResult.GeneratedTrees, Has.Length.EqualTo(2), "Only the unrelated valid class and its serializer should be emitted.");
			Assert.That(result.Output.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error && item.Location.SourceTree != result.InputTree), Is.Empty);
			Assert.That(result.Output.GetTypeByMetadataName("Good")!.GetMembers("Value").OfType<IPropertySymbol>().Single().PartialImplementationPart, Is.Not.Null);
		});
	}

	private static void AssertCompiles(RunResult result) {
		Assert.Multiple(() => {
			Assert.That(result.InputAnalyzerDiagnostics, Is.Empty);
			Assert.That(result.OutputAnalyzerDiagnostics, Is.Empty);
			Assert.That(result.GeneratorDiagnostics, Is.Empty);
			Assert.That(result.Output.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error), Is.Empty);
		});
	}

	private static int Execute(RunResult result) {
		using var stream = new MemoryStream();
		var emitted = result.Output.Emit(stream);
		Assert.That(emitted.Success, Is.True, string.Join(Environment.NewLine, emitted.Diagnostics));
		return (int)Assembly.Load(stream.ToArray()).GetType("Probe")!.GetMethod("Run")!.Invoke(null, null)!;
	}

	private static async Task<RunResult> RunAsync(string source) {
		SyntaxTree inputTree = CSharpSyntaxTree.ParseText("#nullable enable\nusing System; using Cat.Network;\n" + source, path: "Input.cs");
		var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
			.Append(typeof(NetworkObject).Assembly.Location).Distinct().Select(path => MetadataReference.CreateFromFile(path));
		CSharpCompilation input = CSharpCompilation.Create("TypeIdentity_" + Guid.NewGuid().ToString("N"), [inputTree], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
		ImmutableArray<DiagnosticAnalyzer> analyzers = [new CatNetworkAnalyzer()];
		ImmutableArray<Diagnostic> inputDiagnostics = await input.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());
		driver = driver.RunGeneratorsAndUpdateCompilation(input, out Compilation output, out ImmutableArray<Diagnostic> generatorDiagnostics);
		ImmutableArray<Diagnostic> outputDiagnostics = await output.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
		Assert.That(inputDiagnostics.Concat(generatorDiagnostics).Concat(outputDiagnostics).Where(item => item.Id is "CS8785" or "AD0001"), Is.Empty);
		return new RunResult(inputTree, output, driver.GetRunResult(), generatorDiagnostics, inputDiagnostics, outputDiagnostics);
	}

	private sealed record RunResult(SyntaxTree InputTree, Compilation Output, GeneratorDriverRunResult GeneratorResult,
		ImmutableArray<Diagnostic> GeneratorDiagnostics, ImmutableArray<Diagnostic> InputAnalyzerDiagnostics, ImmutableArray<Diagnostic> OutputAnalyzerDiagnostics);
}
