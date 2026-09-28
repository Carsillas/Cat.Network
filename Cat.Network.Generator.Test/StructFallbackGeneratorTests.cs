using System.Collections.Immutable;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Cat.Network.Generator.Test;

public sealed class StructFallbackGeneratorTests {
	[TestCase("decimal")]
	[TestCase("System.DateTime")]
	[TestCase("char")]
	[TestCase("decimal?")]
	[TestCase("System.DateTime?")]
	[TestCase("char?")]
	[TestCase("Nested<decimal>")]
	[TestCase("Nested<Nested<System.DateTime>>")]
	[TestCase("Nested<char?>")]
	[TestCase("Hidden")]
	[TestCase("AutoProperty")]
	public void UnsupportedPropertiesFailInsteadOfWritingOrReadingEmptyValuesWhenAnalyzerIsAbsent(string typeName) {
		string source = $$"""
			using Cat.Network;
			public struct Nested<T> { public T Value; }
			public struct Hidden { private int value; public int Value => value; }
			public struct AutoProperty { public int Value { get; set; } }
			[NetworkObject]
			public partial class State : NetworkObject {
				[NetworkProperty] public partial {{typeName}} Value { get; set; }
			}
			""";
		(Assembly assembly, _) = Generate(source);
		Type stateType = assembly.GetType("State")!;
		NetworkObject state = (NetworkObject)Activator.CreateInstance(stateType)!;
		TypeCatalogue catalogue = new();
		catalogue.Register(stateType);
		Assert.That(catalogue.TryFindSerializer(stateType, out INetworkObjectSerializer? serializer), Is.True);
		SerializationContext context = new(catalogue);
		Assert.Multiple(() => {
			Assert.That(() => serializer!.Serialize(new BufferWriter(), state, context, new SerializationOptions(MemberSelectionMode.All, MemberIdentificationMode.Index)),
				Throws.InvalidOperationException.With.Message.Contains("not supported"));
			// Schema zero, index mode, one property (index zero) with an empty value.
			Assert.That(() => serializer!.Deserialize(state, new byte[] { 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0 }, context),
				Throws.InvalidOperationException.With.Message.Contains("not supported"));
		});
	}

	[TestCase("decimal")]
	[TestCase("System.DateTime")]
	[TestCase("char")]
	[TestCase("decimal?")]
	[TestCase("System.DateTime?")]
	[TestCase("char?")]
	[TestCase("Nested<decimal>")]
	[TestCase("Nested<Nested<System.DateTime>>")]
	[TestCase("Nested<char?>")]
	public void UnsupportedMessageParametersCannotDispatchDefaultValuesWhenAnalyzerIsAbsent(string typeName) {
		string source = $$"""
			using Cat.Network;
			public struct Nested<T> { public T Value; }
			[NetworkObject]
			public partial class State : NetworkEntity {
				[RPC] public partial void Send({{typeName}} value);
				[Broadcast] public partial void Share({{typeName}} value);
			}
			""";
		(Assembly assembly, GeneratorDriverRunResult result) = Generate(source);
		INetworkRpcTarget target = (INetworkRpcTarget)Activator.CreateInstance(assembly.GetType("State")!)!;
		string messages = result.GeneratedTrees.Single(tree => tree.FilePath.EndsWith("State_Messages.g.cs", StringComparison.Ordinal)).GetText().ToString();
		ulong[] ids = Regex.Matches(messages, @"case (\d+)UL:").Select(match => ulong.Parse(match.Groups[1].Value)).ToArray();
		SerializationContext context = new(new TypeCatalogue());
		Assert.Multiple(() => {
			Assert.That(messages, Does.Contain("Network message parameter type is not supported."));
			Assert.That(ids, Has.Length.EqualTo(2));
			Assert.That(target.TryInvokeRpc(null!, null!, ids[0], new byte[4], context), Is.False);
			Assert.That(target.TryInvokeBroadcast(null!, null!, ids[1], new byte[4], context), Is.False);
			Assert.That(target.TryInvokeRpc(null!, null!, ids[0], new byte[] { 1, 0, 0, 0, 0 }, context), Is.False);
			Assert.That(target.TryInvokeBroadcast(null!, null!, ids[1], new byte[] { 1, 0, 0, 0, 0 }, context), Is.False);
		});
	}

	[Test]
	public void EmptyStructMessageParametersPreserveFollowingScalarValues() {
		const string source = """
			using Cat.Network;
			public struct Empty { }
			[NetworkObject]
			public partial class State : NetworkEntity {
				[RPC] public partial void Send(Empty empty, int value);
				public int Received;
				public State() { SendReceived += (_, _, _, value) => Received = value; }
			}
			""";
		(Assembly assembly, GeneratorDriverRunResult result) = Generate(source);
		INetworkRpcTarget target = (INetworkRpcTarget)Activator.CreateInstance(assembly.GetType("State")!)!;
		string messages = result.GeneratedTrees.Single(tree => tree.FilePath.EndsWith("State_Messages.g.cs", StringComparison.Ordinal)).GetText().ToString();
		ulong id = ulong.Parse(Regex.Match(messages, @"case (\d+)UL:").Groups[1].Value);
		BufferWriter writer = new();
		writer.WriteInt32(0);
		writer.WriteInt32(4);
		writer.WriteInt32(42);
		Assert.That(target.TryInvokeRpc(null!, null!, id, writer.GetWrittenSpan(), new SerializationContext(new TypeCatalogue())), Is.True);
		Assert.That(target.GetType().GetField("Received")!.GetValue(target), Is.EqualTo(42));
	}

	private static (Assembly Assembly, GeneratorDriverRunResult Result) Generate(string source) {
		IEnumerable<MetadataReference> references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
			.Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path))
			.Append(MetadataReference.CreateFromFile(typeof(NetworkObject).Assembly.Location));
		CSharpCompilation compilation = CSharpCompilation.Create("StructFallback_" + Guid.NewGuid().ToString("N"), [CSharpSyntaxTree.ParseText(source)], references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out Compilation output, out ImmutableArray<Diagnostic> diagnostics);
		Assert.That(diagnostics, Is.Empty);
		using MemoryStream stream = new();
		Microsoft.CodeAnalysis.Emit.EmitResult emit = output.Emit(stream);
		Assert.That(emit.Success, Is.True, string.Join(Environment.NewLine, emit.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
		return (Assembly.Load(stream.ToArray()), driver.GetRunResult());
	}
}
