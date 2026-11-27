using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using Cat.Network.Analyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Cat.Network.Generator.Test;

public sealed class NetworkMessageOverloadTests {
	[TestCase("RPC")]
	[TestCase("Broadcast")]
	public async Task DefaultOverloads_CompileAndDispatchToDistinctEvents(string kind) {
		Compilation compilation = await Generate($$"""
			[NetworkObject] public partial class Entity : NetworkEntity {
				[{{kind}}] public partial void Update(int amount);
				[{{kind}}] public partial void Update(string text);
			}
			""");
		Assembly assembly = Emit(compilation);
		NetworkEntity entity = CreateEntity(assembly);
		Receiver receiver = new();
		Subscribe(entity, QualifiedName("global::System.Int32"), receiver, nameof(Receiver.OnInteger));
		Subscribe(entity, QualifiedName("global::System.String"), receiver, nameof(Receiver.OnText));

		AssertDispatches(assembly, entity, receiver, kind, kind);
		Assert.That(entity.GetType().GetEvent("UpdateReceived"), Is.Null);
	}

	[Test]
	public async Task SameNameRpcAndBroadcast_CompileAndDispatchToDistinctEvents() {
		Compilation compilation = await Generate("""
			[NetworkObject] public partial class Entity : NetworkEntity {
				[RPC] public partial void Update(int amount);
				[Broadcast] public partial void Update(string text);
			}
			""");
		Assembly assembly = Emit(compilation);
		NetworkEntity entity = CreateEntity(assembly);
		Receiver receiver = new();
		Subscribe(entity, QualifiedName("global::System.Int32"), receiver, nameof(Receiver.OnInteger));
		Subscribe(entity, QualifiedName("global::System.String"), receiver, nameof(Receiver.OnText));

		AssertDispatches(assembly, entity, receiver, "RPC", "Broadcast");
	}

	[TestCase("RPC")]
	[TestCase("Broadcast")]
	public async Task SingleEvent_WithOrdinaryAndExplicitOverloads_KeepsExistingReceiveNames(string kind) {
		Compilation compilation = await Generate($$"""
			[NetworkObject] public partial class Entity : NetworkEntity {
				[{{kind}}] public partial void Update(int amount);
				[{{kind}}(NetworkMessageReceiveMode.Explicit)] public partial void Update(string text);
				public void Update(bool enabled) { }
				public string LastText = "";
				void Entity.{{kind}}.Update(RelayClient client, NetworkProfile instigator, string text) => LastText = text;
			}
			""");
		Assembly assembly = Emit(compilation);
		NetworkEntity entity = CreateEntity(assembly);
		Receiver receiver = new();
		Subscribe(entity, "Update", receiver, nameof(Receiver.OnInteger));
		Type receiveInterface = entity.GetType().GetNestedType(kind)!;
		Assert.Multiple(() => {
			Assert.That(entity.GetType().GetNestedType("UpdateRpcHandler"), Is.Not.Null);
			Assert.That(receiveInterface.GetEvent("UpdateReceived"), Is.Not.Null);
			Assert.That(receiveInterface.GetMethod("RaiseUpdate"), Is.Not.Null);
			Assert.That(receiveInterface.GetMethod("Update"), Is.Not.Null);
		});

		RelayClient client = new(new TypeCatalogue());
		NetworkProfile profile = CreateProfile(assembly);
		Assert.That(Dispatch(entity, kind, client, profile, 42), Is.True);
		Assert.That(Dispatch(entity, kind, client, profile, "hello"), Is.True);
		Assert.Multiple(() => {
			Assert.That(receiver.Values, Is.EqualTo(new object[] { 42 }));
			Assert.That(entity.GetType().GetField("LastText")!.GetValue(entity), Is.EqualTo("hello"));
		});
	}

	[TestCase("RPC")]
	[TestCase("Broadcast")]
	public async Task ExplicitOverloads_KeepReceiveMethodsAndDispatchBothSignatures(string kind) {
		Compilation compilation = await Generate($$"""
			[NetworkObject] public partial class Entity : NetworkEntity {
				[{{kind}}(NetworkMessageReceiveMode.Explicit)] public partial void Update(int amount);
				[{{kind}}(NetworkMessageReceiveMode.Explicit)] public partial void Update(string text);
				public int LastInteger;
				public string LastText = "";
				void Entity.{{kind}}.Update(RelayClient client, NetworkProfile instigator, int amount) => LastInteger = amount;
				void Entity.{{kind}}.Update(RelayClient client, NetworkProfile instigator, string text) => LastText = text;
			}
			""");
		Assembly assembly = Emit(compilation);
		NetworkEntity entity = CreateEntity(assembly);
		RelayClient client = new(new TypeCatalogue());
		NetworkProfile profile = CreateProfile(assembly);
		Assert.That(Dispatch(entity, kind, client, profile, 42), Is.True);
		Assert.That(Dispatch(entity, kind, client, profile, "hello"), Is.True);
		Assert.Multiple(() => {
			Assert.That(entity.GetType().GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly), Is.Empty);
			Assert.That(entity.GetType().GetNestedType(kind)!.GetMethods().Select(method => method.Name), Is.EqualTo(new[] { "Update", "Update" }));
			Assert.That(entity.GetType().GetField("LastInteger")!.GetValue(entity), Is.EqualTo(42));
			Assert.That(entity.GetType().GetField("LastText")!.GetValue(entity), Is.EqualTo("hello"));
		});
	}

	[TestCase("RPC", "RPC")]
	[TestCase("Broadcast", "Broadcast")]
	[TestCase("RPC", "Broadcast")]
	[TestCase("Broadcast", "RPC")]
	public async Task InheritedSingleEvent_KeepsItsNameWhenDerivedTypeAddsAnOverload(string baseKind, string derivedKind) {
		Compilation compilation = await Generate($$"""
			[NetworkObject] public abstract partial class BaseEntity : NetworkEntity {
				[{{baseKind}}] public partial void Update(int amount);
			}
			[NetworkObject] public abstract partial class MiddleEntity : BaseEntity { }
			[NetworkObject] public partial class Entity : MiddleEntity {
				[{{derivedKind}}] public partial void Update(string text);
			}
			""");
		Assembly assembly = Emit(compilation);
		NetworkEntity entity = CreateEntity(assembly);
		Receiver receiver = new();
		Subscribe(entity, "Update", receiver, nameof(Receiver.OnInteger));
		Subscribe(entity, QualifiedName("global::System.String"), receiver, nameof(Receiver.OnText));

		AssertDispatches(assembly, entity, receiver, baseKind, derivedKind);
		Assert.That(entity.GetType().GetEvent("UpdateReceived")!.DeclaringType!.Name, Is.EqualTo("BaseEntity"));
	}

	[TestCase("RPC")]
	[TestCase("Broadcast")]
	public async Task InheritedOverloads_FromReferencedAssembly_KeepTheirReceiveNames(string kind) {
		Compilation baseCompilation = await Generate($$"""
			[NetworkObject] public abstract partial class BaseEntity : NetworkEntity {
				[{{kind}}] public partial void Update(int amount);
				[{{kind}}] public partial void Update(string text);
			}
			""", includeProfile: false);
		byte[] baseImage = EmitBytes(baseCompilation);
		LoadAssembly(baseImage);
		Compilation compilation = await Generate($$"""
			[NetworkObject] public abstract partial class MiddleEntity : BaseEntity { }
			[NetworkObject] public partial class Entity : MiddleEntity {
				[{{kind}}] public partial void Refresh();
			}
			""", reference: MetadataReference.CreateFromImage(baseImage));
		Assembly assembly = Emit(compilation);
		NetworkEntity entity = CreateEntity(assembly);
		Receiver receiver = new();
		Subscribe(entity, QualifiedName("global::System.Int32"), receiver, nameof(Receiver.OnInteger));
		Subscribe(entity, QualifiedName("global::System.String"), receiver, nameof(Receiver.OnText));

		AssertDispatches(assembly, entity, receiver, kind, kind);
		Assert.That(entity.GetType().GetEvent("RefreshReceived"), Is.Not.Null);
	}

	[TestCase("RPC")]
	[TestCase("Broadcast")]
	public async Task InheritedOverloads_FromLegacyAssembly_KeepItsUnqualifiedReceiveApi(string kind) {
		Compilation legacyCompilation = await Generate($$"""
			[NetworkObject] public abstract partial class BaseEntity : NetworkEntity {
				[{{kind}}] public partial void Update(int amount);
			}
			[NetworkObject] public abstract partial class MiddleEntity : BaseEntity {
				[{{kind}}] public partial void Update(string text);
			}
			""", includeProfile: false);
		// The earlier generator emitted unqualified events on both inheritance levels.
		// Recreate that compiled public API before testing a consumer of the legacy library.
		foreach (SyntaxTree tree in legacyCompilation.SyntaxTrees.ToArray()) {
			string source = tree.ToString();
			if (source.Contains(QualifiedName("global::System.String"), StringComparison.Ordinal)) {
				legacyCompilation = legacyCompilation.ReplaceSyntaxTree(tree, CSharpSyntaxTree.ParseText(
					source.Replace(QualifiedName("global::System.String"), "Update", StringComparison.Ordinal),
					(CSharpParseOptions)tree.Options,
					tree.FilePath));
			}
		}
		byte[] legacyImage = EmitBytes(legacyCompilation);
		Assembly legacyAssembly = LoadAssembly(legacyImage);
		Compilation compilation = await Generate($$"""
			[NetworkObject] public partial class Entity : MiddleEntity {
				[{{kind}}] public partial void Refresh();
			}
			""", reference: MetadataReference.CreateFromImage(legacyImage));
		Assembly assembly = Emit(compilation);
		NetworkEntity entity = CreateEntity(assembly);
		Receiver receiver = new();
		Subscribe(entity, "Update", receiver, nameof(Receiver.OnInteger), legacyAssembly.GetType("Game.BaseEntity"));
		Subscribe(entity, "Update", receiver, nameof(Receiver.OnText), legacyAssembly.GetType("Game.MiddleEntity"));

		AssertDispatches(assembly, entity, receiver, kind, kind);
	}

	[Test]
	public async Task QualifiedNames_AvoidASeparateMessageWithTheSameStem() {
		string conflictingName = QualifiedName("global::System.Int32");
		Compilation compilation = await Generate($$"""
			[NetworkObject] public partial class Entity : NetworkEntity {
				[RPC] public partial void Update(int amount);
				[RPC] public partial void Update(string text);
				[Broadcast] public partial void {{conflictingName}}();
			}
			""");
		Assembly assembly = Emit(compilation);
		NetworkEntity entity = CreateEntity(assembly);
		Receiver receiver = new();
		Subscribe(entity, conflictingName + "_", receiver, nameof(Receiver.OnInteger));
		Subscribe(entity, QualifiedName("global::System.String"), receiver, nameof(Receiver.OnText));

		AssertDispatches(assembly, entity, receiver, "RPC", "RPC");
		Assert.That(entity.GetType().GetEvent(conflictingName + "Received")!.EventHandlerType!.GetMethod("Invoke")!.GetParameters(), Has.Length.EqualTo(2));
	}

	[TestCase("Received")]
	[TestCase("RpcHandler")]
	[TestCase("Raise")]
	public async Task QualifiedNames_AvoidGeneratedMemberCollisionsWithOtherMessages(string member) {
		string stem = QualifiedName("global::System.Int32");
		string conflictingName = member == "Raise" ? "Raise" + stem : stem + member;
		Compilation compilation = await Generate($$"""
			[NetworkObject] public partial class Entity : NetworkEntity {
				[RPC] public partial void Update(int amount);
				[RPC] public partial void Update(string text);
				[RPC(NetworkMessageReceiveMode.Explicit)] public partial void {{conflictingName}}(int amount);
				void Entity.RPC.{{conflictingName}}(RelayClient client, NetworkProfile instigator, int amount) { }
			}
			""");
		Assembly assembly = Emit(compilation);
		NetworkEntity entity = CreateEntity(assembly);
		Receiver receiver = new();
		Subscribe(entity, stem + "_", receiver, nameof(Receiver.OnInteger));
		Subscribe(entity, QualifiedName("global::System.String"), receiver, nameof(Receiver.OnText));

		AssertDispatches(assembly, entity, receiver, "RPC", "RPC");
	}

	[Test]
	public async Task InheritedQualifiedName_IsReservedForItsOriginalEvent() {
		string stem = QualifiedName("global::System.Int32");
		Compilation compilation = await Generate($$"""
			[NetworkObject] public abstract partial class BaseEntity : NetworkEntity {
				[RPC] public partial void Update(int amount);
				[RPC] public partial void Update(string text);
			}
			[NetworkObject] public partial class Entity : BaseEntity {
				[Broadcast] public partial void {{stem}}();
			}
			""");
		Assembly assembly = Emit(compilation);
		NetworkEntity entity = CreateEntity(assembly);
		Receiver receiver = new();
		Subscribe(entity, stem, receiver, nameof(Receiver.OnInteger));
		Subscribe(entity, QualifiedName("global::System.String"), receiver, nameof(Receiver.OnText));

		AssertDispatches(assembly, entity, receiver, "RPC", "RPC");
		string derivedStem = stem + "_" + BitConverter.ToUInt64(SHA256.HashData(Encoding.UTF8.GetBytes(stem + "()"))).ToString("X16");
		Assert.That(entity.GetType().GetEvent(derivedStem + "Received")!.DeclaringType, Is.EqualTo(entity.GetType()));
	}

	[Test]
	public async Task OverloadNames_AreStableAcrossDeclarationOrderAndUnrelatedOverloads() {
		const string integer = "[RPC] public partial void Update(int amount);";
		const string text = "[RPC] public partial void Update(string text);";
		Compilation first = await Generate($$"""
			[NetworkObject] public partial class Entity : NetworkEntity { {{integer}} {{text}} }
			""");
		Compilation second = await Generate($$"""
			[NetworkObject] public partial class Entity : NetworkEntity { {{text}} [RPC] public partial void Update(); {{integer}} }
			""");
		string[] expected = [QualifiedName("global::System.Int32") + "Received", QualifiedName("global::System.String") + "Received"];
		foreach (Compilation compilation in new[] { first, second }) {
			INamedTypeSymbol entity = compilation.GetTypeByMetadataName("Game.Entity")!;
			Assert.That(entity.GetMembers().OfType<IEventSymbol>().Select(member => member.Name), Is.SupersetOf(expected));
		}
	}

	[Test]
	public async Task Overloads_WithEqualShortTypeNames_UseTheFullSignature() {
		Compilation compilation = await Generate("""
			namespace Left { public struct Value { public int Amount; } }
			namespace Right { public struct Value { public int Amount; } }
			[NetworkObject] public partial class Entity : NetworkEntity {
				[RPC] public partial void Update(Left.Value amount);
				[RPC] public partial void Update(Right.Value amount);
			}
			""");
		INamedTypeSymbol entity = compilation.GetTypeByMetadataName("Game.Entity")!;
		Assert.That(entity.GetMembers().OfType<IEventSymbol>().Select(member => member.Name), Is.EquivalentTo(new[] {
			QualifiedName("global::Game.Left.Value") + "Received",
			QualifiedName("global::Game.Right.Value") + "Received"
		}));
	}

	private static async Task<Compilation> Generate(string declarations, bool includeProfile = true, MetadataReference? reference = null) {
		string source = "#nullable enable\nusing Cat.Network;\nnamespace Game {\n" + declarations +
			(includeProfile ? "\n[NetworkObject] public partial class Profile : NetworkProfile { }\n" : "") + "}";
		IEnumerable<MetadataReference> references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
			.Split(Path.PathSeparator)
			.Append(typeof(NetworkObject).Assembly.Location)
			.Distinct()
			.Select(path => MetadataReference.CreateFromFile(path));
		if (reference is not null) {
			references = references.Append(reference);
		}
		CSharpCompilation input = CSharpCompilation.Create(
			"MessageOverloads_" + Guid.NewGuid().ToString("N"),
			[CSharpSyntaxTree.ParseText(source)],
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
		ImmutableArray<DiagnosticAnalyzer> analyzers = [new CatNetworkAnalyzer()];
		Assert.That(await input.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync(), Is.Empty, "Input analyzer diagnostics");
		GeneratorDriver driver = CSharpGeneratorDriver.Create(new CatNetworkGenerator());
		driver.RunGeneratorsAndUpdateCompilation(input, out Compilation output, out ImmutableArray<Diagnostic> diagnostics);
		Assert.That(diagnostics, Is.Empty, "Generator diagnostics");
		Assert.That(await output.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync(), Is.Empty, "Generated compilation analyzer diagnostics");
		Assert.That(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty, "Compiler errors");
		return output;
	}

	private static byte[] EmitBytes(Compilation compilation) {
		using MemoryStream stream = new();
		Microsoft.CodeAnalysis.Emit.EmitResult result = compilation.Emit(stream);
		Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Diagnostics));
		return stream.ToArray();
	}

	private static Assembly LoadAssembly(byte[] image) {
		using MemoryStream stream = new(image);
		return AssemblyLoadContext.Default.LoadFromStream(stream);
	}

	private static Assembly Emit(Compilation compilation) => LoadAssembly(EmitBytes(compilation));
	private static NetworkEntity CreateEntity(Assembly assembly) => (NetworkEntity)Activator.CreateInstance(assembly.GetType("Game.Entity")!)!;
	private static NetworkProfile CreateProfile(Assembly assembly) => (NetworkProfile)Activator.CreateInstance(assembly.GetType("Game.Profile")!)!;

	private static void Subscribe(NetworkEntity entity, string stem, Receiver receiver, string handler, Type? declaringType = null) {
		EventInfo? received = (declaringType ?? entity.GetType()).GetEvent(stem + "Received");
		Assert.That(received, Is.Not.Null, "Missing receive event " + stem + "Received");
		received!.AddEventHandler(entity, Delegate.CreateDelegate(received.EventHandlerType!, receiver, handler));
	}

	private static void AssertDispatches(Assembly assembly, NetworkEntity entity, Receiver receiver, string integerKind, string textKind) {
		RelayClient client = new(new TypeCatalogue());
		NetworkProfile profile = CreateProfile(assembly);
		Assert.That(Dispatch(entity, integerKind, client, profile, 42), Is.True);
		Assert.That(Dispatch(entity, textKind, client, profile, "hello"), Is.True);
		Assert.Multiple(() => {
			Assert.That(receiver.Values, Is.EqualTo(new object[] { 42, "hello" }));
			Assert.That(receiver.Clients, Has.All.SameAs(client));
			Assert.That(receiver.Profiles, Has.All.SameAs(profile));
		});
	}

	private static bool Dispatch(NetworkEntity entity, string kind, RelayClient client, NetworkProfile profile, object value) {
		BufferWriter writer = new();
		string parameterType;
		if (value is int number) {
			parameterType = "global::System.Int32";
			writer.WriteInt32(sizeof(int));
			writer.WriteInt32(number);
		} else {
			parameterType = "global::System.String";
			string text = (string)value;
			writer.WriteInt32(Encoding.UTF8.GetByteCount(text));
			writer.WriteUtf8(text);
		}
		ulong id = MessageId(parameterType);
		INetworkRpcTarget target = entity;
		return kind == "RPC"
			? target.TryInvokeRpc(client, profile, id, writer.GetWrittenSpan(), new SerializationContext(new TypeCatalogue()))
			: target.TryInvokeBroadcast(client, profile, id, writer.GetWrittenSpan(), new SerializationContext(new TypeCatalogue()));
	}

	private static ulong MessageId(string parameterType) => BitConverter.ToUInt64(SHA256.HashData(Encoding.UTF8.GetBytes("Update(" + parameterType + ")")));
	private static string QualifiedName(string parameterType) => "Update_" + MessageId(parameterType).ToString("X16");

	private sealed class Receiver {
		public List<object> Values { get; } = [];
		public List<RelayClient> Clients { get; } = [];
		public List<NetworkProfile> Profiles { get; } = [];
		public void OnInteger(RelayClient client, NetworkProfile profile, int value) => Receive(client, profile, value);
		public void OnText(RelayClient client, NetworkProfile profile, string value) => Receive(client, profile, value);
		private void Receive(RelayClient client, NetworkProfile profile, object value) {
			Values.Add(value);
			Clients.Add(client);
			Profiles.Add(profile);
		}
	}
}
