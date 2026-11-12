using System.Collections.Immutable;
using System.Reflection;
using Cat.Network.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cat.Network.Generator.Test;

public sealed class NetworkDeclarationShapeTests {
	[Test]
	public async Task MessagesPreserveAccessibilityAndDispatch(
		[Values("public", "private", "internal", "protected", "protected internal", "private protected", "")] string accessibility,
		[Values("RPC", "Broadcast")] string attribute,
		[Values(false, true)] bool explicitReceive) {
		string receiveMode = explicitReceive ? "(NetworkMessageReceiveMode.Explicit)" : string.Empty;
		string interfaceName = attribute == "RPC" ? "RPC" : "Broadcast";
		string handler = explicitReceive
			? $"void Entity.{interfaceName}.Update(RelayClient client, NetworkProfile instigator, int amount) {{ Received = amount; }}"
			: "public Entity() { UpdateReceived += (_, _, amount) => Received = amount; }";
		string source = $$"""
			using System;
			using Cat.Network;
			[NetworkObject] public partial class Profile : NetworkProfile { }
			[NetworkObject] public partial class Entity : NetworkEntity {
				[{{attribute}}{{receiveMode}}] {{accessibility}} partial void Update(int amount);
				public int Received;
				{{handler}}
				public void SendForTest() { Update(9); }
			}
			public static class Probe {
				public static int Run() {
					var entity = new Entity();
					bool sendImplemented = false;
					try { entity.SendForTest(); }
					catch (InvalidOperationException ex) { sendImplemented = ex.Message.Contains("connected relay client"); }
					var catalogue = new TypeCatalogue();
					var writer = new BufferWriter();
					writer.WriteInt32(4);
					writer.WriteInt32(42);
					ulong id = BitConverter.ToUInt64(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("Update(global::System.Int32)")));
					bool received = ((INetworkRpcTarget)entity).TryInvoke{{(attribute == "RPC" ? "Rpc" : "Broadcast")}}(new RelayClient(catalogue), new Profile(), id, writer.GetWrittenSpan(), new SerializationContext(catalogue));
					return sendImplemented && received ? entity.Received : -1;
				}
			}
			""";

		CompilationResult result = await Compile(source);
		AssertSupported(result);
		IMethodSymbol method = result.Output.GetTypeByMetadataName("Entity")!.GetMembers("Update").OfType<IMethodSymbol>().Single();
		Assert.That(method.DeclaredAccessibility, Is.EqualTo(GetAccessibility(accessibility)));
		Assert.That(RunProbe(result.Output), Is.EqualTo(42));
	}

	[TestCaseSource(nameof(UnsupportedDeclarations))]
	public async Task UnsupportedDeclarationsHaveSourceDiagnostic(string declaration, string diagnosticId, string diagnosticText) {
		string source = $$"""
			using Cat.Network;
			[NetworkObject] public abstract partial class Entity : NetworkEntity {
				{{declaration}}
			}
			""";
		CompilationResult result = await Compile(source);
		AssertRejected(result, diagnosticId, diagnosticText);
	}

	private static IEnumerable<TestCaseData> UnsupportedDeclarations() {
		yield return new("[NetworkProperty] public static partial int Value { get; set; }", "CN0030", "static");
		yield return new("[NetworkCollection] public static partial NetworkList<int> Values { get; }", "CN0031", "static");
		yield return new("[NetworkCollection] public static partial NetworkDictionary<int, int> Values { get; }", "CN0031", "static");
		yield return new("[NetworkProperty] public partial int Value { get; init; }", "CN0006", "set");
		yield return new("[NetworkProperty] public required partial int Value { get; set; }", "CN0030", "required");
		foreach (string modifier in new[] { "virtual", "abstract", "extern" }) {
			yield return new($"[NetworkProperty] public {modifier} partial int Value {{ get; set; }}", "CN0030", modifier);
			yield return new($"[NetworkCollection] public {modifier} partial NetworkList<int> Values {{ get; }}", "CN0031", modifier);
		}
		foreach (string attribute in new[] { "RPC", "Broadcast" }) {
			foreach (string modifier in new[] { "static", "virtual", "abstract", "extern" }) {
				yield return new($"[{attribute}] public {modifier} partial void Update(int amount);", "CN0022", "instance");
			}
		}
	}

	[TestCase("public abstract int Value { get; set; }", "[NetworkProperty] public override partial int Value { get; set; }", "CN0030")]
	[TestCase("public abstract NetworkList<int> Values { get; }", "[NetworkCollection] public override partial NetworkList<int> Values { get; }", "CN0031")]
	[TestCase("public abstract void Update(int amount);", "[RPC] public override partial void Update(int amount);", "CN0022")]
	[TestCase("public abstract void Update(int amount);", "[Broadcast] public sealed override partial void Update(int amount);", "CN0022")]
	public async Task OverridesHaveSourceDiagnostic(string baseMember, string declaration, string diagnosticId) {
		string source = $$"""
			using Cat.Network;
			[NetworkObject] public abstract partial class Parent : NetworkEntity {
				{{baseMember}}
			}
			[NetworkObject] public abstract partial class Entity : Parent {
				{{declaration}}
			}
			""";
		CompilationResult result = await Compile(source);
		AssertRejected(result, diagnosticId, diagnosticId == "CN0022" ? "instance" : "override", diagnosticId == "CN0031" ? new[] { "CN0026" } : Array.Empty<string>());
	}

	[Test]
	public async Task RequiredPropertiesDoNotProduceInvalidCloneConstruction([Values(false, true)] bool inherited) {
		string source = $$"""
			using Cat.Network;
			[NetworkObject] public {{(inherited ? "abstract" : string.Empty)}} partial class Entity : NetworkObject {
				[NetworkProperty] public required partial int Value { get; set; }
			}
			{{(inherited ? "[NetworkObject] public partial class Derived : Entity { }" : string.Empty)}}
			[NetworkObject] public partial class Control : NetworkObject {
				[NetworkProperty] public partial int ControlValue { get; set; }
			}
			""";
		CompilationResult result = await Compile(source);
		AssertRejected(result, "CN0030", "required");
		Assert.That(result.Output.GetTypeByMetadataName("Control")!.GetMembers("Clone"), Has.Length.EqualTo(1));
	}

	[Test]
	public async Task ReadonlyStructFieldsAreRejectedForPropertiesAndMessages(
		[Values("[NetworkProperty] public partial Payload Data { get; set; }", "[RPC] public partial void Update(Payload amount);", "[Broadcast] public partial void Update(Payload amount);")] string declaration,
		[Values(false, true)] bool nested,
		[Values(false, true)] bool nullable) {
		string payload = nested ? "public struct Payload { public Leaf Value; } public struct Leaf { public readonly int Amount; }" : "public struct Payload { public readonly int Amount; }";
		if (nullable) {
			declaration = declaration.Replace("Payload ", "Payload? ");
			payload = payload.Replace("public Leaf ", "public Leaf? ");
		}
		string source = $$"""
			using Cat.Network;
			{{payload}}
			[NetworkObject] public partial class Entity : NetworkEntity {
				{{declaration}}
			}
			""";
		CompilationResult result = await Compile(source);
		AssertRejected(result, declaration.StartsWith("[NetworkProperty]", StringComparison.Ordinal) ? "CN0028" : "CN0023", "readonly");
	}

	[Test]
	public async Task PrivatePropertiesAndRestrictedAccessorsStillRoundTrip([Values("private", "")] string accessibility) {
		string source = """
			using Cat.Network;
			public struct Payload { public int Amount; public static readonly int DefaultAmount = 7; }
			[NetworkObject] public abstract partial class Parent : NetworkObject {
				[NetworkProperty] private partial int Secret { get; set; }
				[NetworkCollection] private partial NetworkList<int> Values { get; }
				protected void SetPrivate() { Secret = 13; Values.Add(17); }
				public int ReadPrivate() => Secret + Values[0];
			}
			[NetworkObject] public partial class Entity : Parent {
				[NetworkProperty] public partial Payload Data { get; private set; }
				[NetworkProperty] public partial int Code { private get; set; }
				public void SetValues() { SetPrivate(); Data = new Payload { Amount = 19 }; Code = 23; }
				public int ReadValues() => ReadPrivate() + Data.Amount + Code;
			}
			public static class Probe {
				public static int Run() {
					var catalogue = new TypeCatalogue();
					catalogue.Register(typeof(Entity));
					catalogue.TryFindSerializer(typeof(Entity), out var serializer);
					var source = new Entity();
					source.SetValues();
					var writer = new BufferWriter();
					var context = new SerializationContext(catalogue);
					serializer!.Serialize(writer, source, context, new SerializationOptions(MemberSelectionMode.All, MemberIdentificationMode.Name));
					var destination = new Entity();
					serializer.Deserialize(destination, writer.GetWrittenSpan(), context);
					return destination.ReadValues();
				}
			}
			""";
		source = source.Replace("[NetworkProperty] private partial", $"[NetworkProperty] {accessibility} partial")
			.Replace("[NetworkCollection] private partial", $"[NetworkCollection] {accessibility} partial");
		CompilationResult result = await Compile(source);
		AssertSupported(result);
		Assert.That(RunProbe(result.Output), Is.EqualTo(72));
	}

	[Test]
	public async Task ReadonlyCollectionFieldsRemainSupportedByReflectionCodec() {
		const string source = """
			using Cat.Network;
			public readonly struct Payload {
				public readonly int Amount;
				public Payload(int amount) { Amount = amount; }
			}
			[NetworkObject] public partial class Entity : NetworkObject {
				[NetworkCollection] public partial NetworkList<Payload> Values { get; }
				[NetworkCollection] public partial NetworkDictionary<Payload, Payload> Mapping { get; }
			}
			public static class Probe {
				public static int Run() {
					var catalogue = new TypeCatalogue();
					catalogue.Register(typeof(Entity));
					catalogue.TryFindSerializer(typeof(Entity), out var serializer);
					var source = new Entity();
					source.Values.Add(new Payload(17));
					source.Mapping.Add(new Payload(19), new Payload(23));
					var writer = new BufferWriter();
					var context = new SerializationContext(catalogue);
					serializer!.Serialize(writer, source, context, new SerializationOptions(MemberSelectionMode.All, MemberIdentificationMode.Name));
					var destination = new Entity();
					serializer.Deserialize(destination, writer.GetWrittenSpan(), context);
					return destination.Values[0].Amount + destination.Mapping[new Payload(19)].Amount;
				}
			}
			""";
		CompilationResult result = await Compile(source);
		AssertSupported(result);
		Assert.That(RunProbe(result.Output), Is.EqualTo(40));
	}

	[TestCase("public int Value { get; set; }", "[NetworkProperty] public new partial int Value { get; set; }", "var entity = new Entity { Value = 17 }; return entity.Clone().Value;")]
	[TestCase("public NetworkObject? Child { get; set; }", "[NetworkProperty] public new partial Payload? Child { get; set; }", "var entity = new Entity { Child = new Payload { Amount = 17 } }; return entity.Clone().Child!.Amount;")]
	[TestCase("public int Values;", "[NetworkCollection] public new partial NetworkList<int> Values { get; }", "var entity = new Entity(); entity.Values.Add(17); return RoundTrip(entity).Values[0];")]
	[TestCase("public void Update(int amount) { }", "[RPC] public new partial void Update(int amount);", "try { new Entity().Update(17); } catch (InvalidOperationException) { return 17; } return -1;")]
	[TestCase("public void Update(int amount) { }", "[Broadcast] public new partial void Update(int amount);", "try { new Entity().Update(17); } catch (InvalidOperationException) { return 17; } return -1;")]
	public async Task ExplicitNewPreservesHidingOfOrdinaryBaseMembers(string baseMember, string declaration, string probeBody) {
		string source = $$"""
			using System;
			using Cat.Network;
			[NetworkObject] public partial class Payload : NetworkObject {
				[NetworkProperty] public partial int Amount { get; set; }
			}
			[NetworkObject] public abstract partial class Parent : NetworkEntity {
				{{baseMember}}
			}
			[NetworkObject] public partial class Entity : Parent {
				{{declaration}}
			}
			public static class Probe {
				public static int Run() { {{probeBody}} }
				private static Entity RoundTrip(Entity source) {
					var catalogue = new TypeCatalogue();
					catalogue.Register(typeof(Entity));
					catalogue.TryFindSerializer(typeof(Entity), out var serializer);
					var writer = new BufferWriter();
					var context = new SerializationContext(catalogue);
					serializer!.Serialize(writer, source, context, new SerializationOptions(MemberSelectionMode.All, MemberIdentificationMode.Name));
					var destination = new Entity();
					serializer.Deserialize(destination, writer.GetWrittenSpan(), context);
					return destination;
				}
			}
			""";
		CompilationResult result = await Compile(source);
		AssertSupported(result);
		Assert.That(result.Output.GetDiagnostics().Where(static diagnostic => diagnostic.Id == "CS0109"), Is.Empty, "The new modifier must not leak onto generated Changed events");
		Assert.That(RunProbe(result.Output), Is.EqualTo(17));
	}

	private static async Task<CompilationResult> Compile(string source) {
		IEnumerable<MetadataReference> references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
			.Split(Path.PathSeparator)
			.Append(typeof(NetworkObject).Assembly.Location)
			.Distinct()
			.Select(static path => MetadataReference.CreateFromFile(path));
		CSharpCompilation input = CSharpCompilation.Create(
			"DeclarationShape_" + Guid.NewGuid().ToString("N"),
			new[] { CSharpSyntaxTree.ParseText(source, path: "Schema.cs") },
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
		ImmutableArray<DiagnosticAnalyzer> analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new CatNetworkAnalyzer());
		ImmutableArray<Diagnostic> inputDiagnostics = await input.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());
		driver.RunGeneratorsAndUpdateCompilation(input, out Compilation output, out ImmutableArray<Diagnostic> generatorDiagnostics);
		ImmutableArray<Diagnostic> outputDiagnostics = await output.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
		return new(output, inputDiagnostics, outputDiagnostics, generatorDiagnostics);
	}

	private static void AssertSupported(CompilationResult result) {
		Assert.Multiple(() => {
			Assert.That(result.InputDiagnostics, Is.Empty, "Input analyzer diagnostics");
			Assert.That(result.OutputDiagnostics, Is.Empty, "Output analyzer diagnostics");
			Assert.That(result.GeneratorDiagnostics, Is.Empty, "Generator diagnostics");
			Assert.That(result.Output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty, "Compilation errors");
		});
	}

	private static void AssertRejected(CompilationResult result, string diagnosticId, string diagnosticText, params string[] additionalDiagnosticIds) {
		string[] expectedDiagnosticIds = new[] { diagnosticId }.Concat(additionalDiagnosticIds).ToArray();
		TestContext.Out.WriteLine($"Input analyzer: {string.Join(", ", result.InputDiagnostics.Select(static diagnostic => diagnostic.Id))}; compilation errors: {string.Join(", ", result.Output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Select(static diagnostic => diagnostic.Id))}");
		Assert.Multiple(() => {
			Assert.That(result.InputDiagnostics.Select(static diagnostic => diagnostic.Id), Is.EquivalentTo(expectedDiagnosticIds), "Input analyzer diagnostics");
			Assert.That(result.OutputDiagnostics.Select(static diagnostic => diagnostic.Id), Is.EquivalentTo(expectedDiagnosticIds), "Output analyzer diagnostics");
			Assert.That(result.InputDiagnostics.Where(diagnostic => diagnostic.Id == diagnosticId).All(diagnostic => diagnostic.Location.SourceTree?.FilePath == "Schema.cs" && diagnostic.GetMessage().Contains(diagnosticText, StringComparison.Ordinal)), Is.True);
			Assert.That(result.GeneratorDiagnostics, Is.Empty);
			Assert.That(result.Output.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Location.SourceTree?.FilePath != "Schema.cs"), Is.Empty, "The generator must not emit invalid implementations for rejected members");
		});
	}

	private static int RunProbe(Compilation compilation) {
		using var stream = new MemoryStream();
		var result = compilation.Emit(stream);
		Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Diagnostics));
		Assembly assembly = Assembly.Load(stream.ToArray());
		return (int)assembly.GetType("Probe")!.GetMethod("Run")!.Invoke(null, null)!;
	}

	private static Accessibility GetAccessibility(string accessibility) => accessibility switch {
		"public" => Accessibility.Public,
		"internal" => Accessibility.Internal,
		"protected" => Accessibility.Protected,
		"protected internal" => Accessibility.ProtectedOrInternal,
		"private protected" => Accessibility.ProtectedAndInternal,
		_ => Accessibility.Private
	};

	private sealed record CompilationResult(Compilation Output, ImmutableArray<Diagnostic> InputDiagnostics, ImmutableArray<Diagnostic> OutputDiagnostics, ImmutableArray<Diagnostic> GeneratorDiagnostics);
}
